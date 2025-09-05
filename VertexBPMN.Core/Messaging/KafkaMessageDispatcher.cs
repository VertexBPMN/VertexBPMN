using System.Collections.Concurrent;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Core.Messaging;

using Confluent.Kafka;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Cmmn;
using VertexBPMN.Core.Exceptions;

/// <summary>
/// Kafka based implementation of IMessageDispatcher.
/// Produces small JSON envelopes to Kafka topics. For subscription style APIs
/// (generic engine messages, case file updates) lightweight dedicated consumers
/// are spawned (Kafka consumer is not thread-safe and cannot be used concurrently).
/// </summary>
public class KafkaMessageDispatcher : IMessageDispatcher, IDisposable
{
    private readonly ILogger<KafkaMessageDispatcher> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly IConsumer<string, string> _consumer; // Used only for legacy case file subscription method (kept for backward compatibility)
    private readonly string _bootstrapServers;

    // Topics (centralized to avoid magic strings)
    private const string CaseTokenTopic = "case-tokens";
    private const string CaseFileUpdateTopic = "case-file-updates";
    private const string ExecutionTokenTopic = "execution-tokens";
    private const string ServiceTaskTopic = "service-tasks";
    private const string UserTaskTopic = "user-tasks";
    private const string TaskQueueTopic = "task-queue";
    private const string DmnTaskRequestTopic = "dmn-task-requests";
    private const string DmnTaskResponseTopic = "dmn-task-responses";
    private const string GenericMessageTopic = "engine-messages";

    // Pending DMN task responses (correlation)
    private readonly ConcurrentDictionary<string, TaskCompletionSource<Dictionary<string, object>>> _dmnPending = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public KafkaMessageDispatcher(ILogger<KafkaMessageDispatcher> logger, string bootstrapServers)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bootstrapServers = bootstrapServers ?? throw new ArgumentNullException(nameof(bootstrapServers));

        var producerConfig = new ProducerConfig { BootstrapServers = _bootstrapServers };
        _producer = new ProducerBuilder<string, string>(producerConfig).Build();

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = "vertex-bpmn-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnablePartitionEof = false
        };
        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }
        
  

    public async Task DispatchServiceTaskAsync(
        string targetWorkerId,
        string implementation,
        Dictionary<string, string> attributes,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetWorkerId)) throw new ArgumentException("Target worker id required", nameof(targetWorkerId));
        if (string.IsNullOrWhiteSpace(implementation)) throw new ArgumentException("Implementation required", nameof(implementation));

        var envelope = new
        {
            kind = "service-dispatch",
            targetWorkerId,
            implementation,
            attributes,
            variables,
            dispatchedAt = DateTime.UtcNow
        };

        var value = JsonSerializer.Serialize(envelope, _jsonOptions);
        try
        {
            await _producer.ProduceAsync(ServiceTaskTopic, new Message<string, string>
            {
                Key = targetWorkerId,
                Value = value
            }, cancellationToken);
            _logger.LogInformation("Dispatched service task implementation {Implementation} for worker {Worker}", implementation, targetWorkerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch service task {Implementation} to worker {Worker}", implementation, targetWorkerId);
            throw new DistributedTokenException($"Failed to dispatch service task {implementation} to worker {targetWorkerId}", ex);
        }
    }

    public async Task PublishTokenAsync(ExecutionToken token, CancellationToken cancellationToken = default)
    {
        if (token == null) throw new ArgumentNullException(nameof(token));
        try
        {
            var value = JsonSerializer.Serialize(token, _jsonOptions);
            await _producer.ProduceAsync(ExecutionTokenTopic, new Message<string, string>
            {
                Key = token.Id.ToString(),
                Value = value
            }, cancellationToken);
            _logger.LogDebug("Published ExecutionToken {TokenId}", token.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish ExecutionToken {TokenId}", token.Id);
            throw new DistributedTokenException($"Failed to publish ExecutionToken {token.Id}", ex);
        }
    }

    public async Task PublishCaseTokenAsync(CaseToken token, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = token.Id.ToString(),
                Value = JsonSerializer.Serialize(token, _jsonOptions)
            };
            await _producer.ProduceAsync(CaseTokenTopic, message, cancellationToken);
            _logger.LogInformation("Published CaseToken {TokenId} to topic {Topic}", token.Id, CaseTokenTopic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish CaseToken {TokenId}", token.Id);
            throw new DistributedTokenException($"Failed to publish CaseToken {token.Id}", ex);
        }
    }

    public async Task QueueTaskAsync(string taskId, string taskType, Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentException("Task id required", nameof(taskId));
        if (string.IsNullOrWhiteSpace(taskType)) throw new ArgumentException("Task type required", nameof(taskType));

        var envelope = new
        {
            kind = "queue-task",
            taskId,
            taskType,
            variables,
            queuedAt = DateTime.UtcNow
        };
        try
        {
            await _producer.ProduceAsync(TaskQueueTopic, new Message<string, string>
            {
                Key = taskId,
                Value = JsonSerializer.Serialize(envelope, _jsonOptions)
            }, cancellationToken);
            _logger.LogDebug("Queued task {TaskId} of type {TaskType}", taskId, taskType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue task {TaskId}", taskId);
            throw new DistributedTokenException($"Failed to queue task {taskId}", ex);
        }
    }

    public async Task DispatchUserTaskAsync(string assignee, string taskId, Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentException("Task id required", nameof(taskId));
        if (string.IsNullOrWhiteSpace(assignee)) throw new ArgumentException("Assignee required", nameof(assignee));

        var envelope = new
        {
            kind = "user-task-dispatch",
            taskId,
            assignee,
            variables,
            dispatchedAt = DateTime.UtcNow
        };
        try
        {
            await _producer.ProduceAsync(UserTaskTopic, new Message<string, string>
            {
                Key = assignee,
                Value = JsonSerializer.Serialize(envelope, _jsonOptions)
            }, cancellationToken);
            _logger.LogInformation("Dispatched user task {TaskId} to {Assignee}", taskId, assignee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch user task {TaskId} to {Assignee}", taskId, assignee);
            throw new DistributedTokenException($"Failed to dispatch user task {taskId} to {assignee}", ex);
        }
    }

    public async Task<Dictionary<string, object>> DispatchDmnTaskAsync(string targetWorker, string decisionRef, Dictionary<string, object> variables,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetWorker)) throw new ArgumentException("Target worker required", nameof(targetWorker));
        if (string.IsNullOrWhiteSpace(decisionRef)) throw new ArgumentException("Decision reference required", nameof(decisionRef));

        var correlationId = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<Dictionary<string, object>>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dmnPending.TryAdd(correlationId, tcs))
            throw new DistributedTokenException("Failed to register DMN correlation id");

        var requestEnvelope = new
        {
            kind = "dmn-request",
            correlationId,
            targetWorker,
            decisionRef,
            variables,
            requestedAt = DateTime.UtcNow
        };

        // Start response listener (short-lived consumer) BEFORE sending (small race window avoidance)
        _ = Task.Run(() => ListenForDmnResponseAsync(correlationId, cancellationToken), cancellationToken);

        try
        {
            await _producer.ProduceAsync(DmnTaskRequestTopic, new Message<string, string>
            {
                Key = targetWorker,
                Value = JsonSerializer.Serialize(requestEnvelope, _jsonOptions)
            }, cancellationToken);
            _logger.LogInformation("Dispatched DMN task {DecisionRef} (Correlation {CorrelationId})", decisionRef, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch DMN task {DecisionRef}", decisionRef);
            _dmnPending.TryRemove(correlationId, out _);
            throw new DistributedTokenException($"Failed to dispatch DMN task {decisionRef}", ex);
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        // Optional: could add timeout here if desired, e.g. linkedCts.CancelAfter(TimeSpan.FromSeconds(30));

        await using var reg = linkedCts.Token.Register(() =>
        {
            if (_dmnPending.TryRemove(correlationId, out var pending))
                pending.TrySetCanceled(linkedCts.Token);
        });

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            throw;
        }
        finally
        {
            _dmnPending.TryRemove(correlationId, out _);
        }
    }

    private async Task ListenForDmnResponseAsync(string correlationId, CancellationToken cancellationToken)
    {
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = $"vertex-bpmn-dmn-resp-{Guid.NewGuid():N}", // unique to allow multiple parallel waits
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(DmnTaskResponseTopic);

        try
        {
            while (!cancellationToken.IsCancellationRequested && _dmnPending.ContainsKey(correlationId))
            {
                ConsumeResult<string, string>? result = null;
                try
                {
                    result = consumer.Consume(TimeSpan.FromMilliseconds(250));
                }
                catch (ConsumeException cex)
                {
                    _logger.LogWarning(cex, "Consume exception on DMN response listener (Correlation {CorrelationId})", correlationId);
                    continue;
                }

                if (result?.Message == null) continue;

                try
                {
                    // Expect envelope with correlationId and resultVariables
                    using var doc = JsonDocument.Parse(result.Message.Value);
                    if (!doc.RootElement.TryGetProperty("correlationId", out var cidProp))
                        continue;
                    if (!string.Equals(cidProp.GetString(), correlationId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Dictionary<string, object> variables = new(StringComparer.OrdinalIgnoreCase);
                    if (doc.RootElement.TryGetProperty("resultVariables", out var varsElem) && varsElem.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in varsElem.EnumerateObject())
                        {
                            variables[prop.Name] = ExtractJsonElement(prop.Value);
                        }
                    }

                    if (_dmnPending.TryRemove(correlationId, out var tcs))
                    {
                        tcs.TrySetResult(variables);
                        _logger.LogInformation("Received DMN response (Correlation {CorrelationId})", correlationId);
                    }
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process DMN response (Correlation {CorrelationId})", correlationId);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in DMN response listener (Correlation {CorrelationId})", correlationId);
            if (_dmnPending.TryRemove(correlationId, out var tcs))
                tcs.TrySetException(new DistributedTokenException($"Failed to receive DMN response for {correlationId}", ex));
        }
        finally
        {
            consumer.Close();
        }

        static object? ExtractJsonElement(JsonElement element) =>
            element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? l :
                                        element.TryGetDouble(out var d) ? d : element.GetRawText(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText(), _jsonOptions),
                JsonValueKind.Array => JsonSerializer.Deserialize<object[]>(element.GetRawText(), _jsonOptions),
                _ => element.GetRawText()
            };
    }

    public async Task SubscribeToMessageAsync(string messageName, Func<Message, Task> handler, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageName)) throw new ArgumentException("Message name required", nameof(messageName));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _bootstrapServers,
            GroupId = $"vertex-bpmn-generic-{messageName}".ToLowerInvariant(),
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(GenericMessageTopic);

        _ = Task.Run(async () =>
        {
            _logger.LogInformation("Subscribed to generic messages '{MessageName}'", messageName);
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ConsumeResult<string, string>? cr = null;
                    try
                    {
                        cr = consumer.Consume(TimeSpan.FromMilliseconds(500));
                    }
                    catch (ConsumeException cex)
                    {
                        _logger.LogWarning(cex, "Consume exception on generic message subscription {MessageName}", messageName);
                        continue;
                    }
                    if (cr?.Message == null) continue;
                    if (!string.Equals(cr.Message.Key, messageName, StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                       
                        var msg = new Message(cr.Message.Key, cr.Message.Value, TryExtractVariables(cr.Message.Value));
                        
                        await handler(msg);
                    }
                    catch (Exception hex)
                    {
                        _logger.LogError(hex, "Error in handler for message {MessageName}", messageName);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in message subscription {MessageName}", messageName);
            }
            finally
            {
                try { consumer.Close(); } catch { /* ignored */ }
                consumer.Dispose();
            }
        }, cancellationToken);
    }

    private static Dictionary<string, object>? TryExtractVariables(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("variables", out var vars) && vars.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in vars.EnumerateObject())
                {
                    dict[p.Name] = p.Value.ValueKind switch
                    {
                        JsonValueKind.String => p.Value.GetString()!,
                        JsonValueKind.Number => p.Value.TryGetInt64(out var l) ? l :
                                                p.Value.TryGetDouble(out var d) ? d : p.Value.GetRawText(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null!,
                        _ => p.Value.GetRawText()
                    };
                }
                return dict;
            }
        }
        catch { /* ignore parse errors */ }
        return null;
    }

    public async Task PublishCaseFileUpdateAsync(CaseFileUpdateEvent updateEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = updateEvent.CaseId,
                Value = JsonSerializer.Serialize(updateEvent, _jsonOptions)
            };
            await _producer.ProduceAsync(CaseFileUpdateTopic, message, cancellationToken);
            _logger.LogInformation("Published CaseFileUpdateEvent for case {CaseId}", updateEvent.CaseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish CaseFileUpdateEvent for case {CaseId}", updateEvent.CaseId);
            throw new DistributedTokenException($"Failed to publish CaseFileUpdateEvent for case {updateEvent.CaseId}", ex);
        }
    }

    public async Task SubscribeToCaseFileUpdateAsync(string caseId, Func<CaseFileUpdateEvent, Task> handler, CancellationToken cancellationToken = default)
    {
        try
        {
            _consumer.Subscribe(CaseFileUpdateTopic);
            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(cancellationToken);
                        if (consumeResult.Message.Key == caseId)
                        {
                            var updateEvent = JsonSerializer.Deserialize<CaseFileUpdateEvent>(consumeResult.Message.Value, _jsonOptions);
                            if (updateEvent != null)
                            {
                                await handler(updateEvent);
                                _logger.LogInformation("Processed CaseFileUpdateEvent for case {CaseId}", caseId);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing CaseFileUpdateEvent for case {CaseId}", caseId);
                    }
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to CaseFileUpdateEvent for case {CaseId}", caseId);
            throw new DistributedTokenException($"Failed to subscribe to CaseFileUpdateEvent for case {caseId}", ex);
        }
    }

    public void Dispose()
    {
        try { _producer?.Flush(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }
        _producer?.Dispose();
        _consumer?.Close();
        _consumer?.Dispose();
    }


}
