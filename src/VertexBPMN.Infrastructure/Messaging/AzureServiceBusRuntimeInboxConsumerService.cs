using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// <para>
/// Azure Service Bus inbox consumer (P2). Uses a <see cref="ServiceBusProcessor"/> in
/// PeekLock mode with <c>AutoCompleteMessages=false</c>, sharing the idempotent
/// <see cref="RuntimeInboxProcessor"/> with the RabbitMQ consumer.
/// </para>
/// <para>
/// Settle semantics:
///  - Completed / CompletedDuplicate -> Complete (abgeschlossene / bereits abgeschlossene Verarbeitung)
///  - Busy / RetryableFailure        -> Abandon (Lock freigeben -> Redelivery; nach MaxDeliveryCount DLQ)
///  - Rejected                       -> DeadLetter (strukturierter Grund)
/// </para>
/// <para>
/// The topic/subscription/queue and the server-side MaxDeliveryCount are provisioned by IaC (the plan
/// requires entities to exist before first publish); the client only connects and settles.
/// </para>
/// </summary>
public sealed class AzureServiceBusRuntimeInboxConsumerService : BackgroundService
{
    private readonly RuntimeOutboxOptions _options;
    private readonly RuntimeInboxOptions _inboxOptions;
    private readonly RuntimeInboxProcessor _processor;
    private readonly ILogger<AzureServiceBusRuntimeInboxConsumerService> _logger;
    private readonly Lock _gate = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2213", Justification = "Disposed in StopAsync.")]
    private ServiceBusClient? _client;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2213", Justification = "Disposed in StopAsync.")]
    private ServiceBusProcessor? _processorHandle;

    public AzureServiceBusRuntimeInboxConsumerService(
        IServiceScopeFactory scopeFactory,
        RuntimeOutboxOptions options,
        RuntimeInboxOptions inboxOptions,
        ILogger<AzureServiceBusRuntimeInboxConsumerService> logger)
    {
        _options = options;
        _inboxOptions = inboxOptions;
        _logger = logger;
        _processor = new RuntimeInboxProcessor(scopeFactory, inboxOptions, NullLogger<RuntimeInboxProcessor>.Instance);
        Validate(out _);
    }

    private void Validate(out string entityPath)
    {
        if (string.IsNullOrWhiteSpace(_options.FullyQualifiedNamespace) && _options.AuthenticationMode != RuntimeOutboxAuthenticationMode.ConnectionString)
		{
			throw new InvalidOperationException(
                "Runtime:Outbox:FullyQualifiedNamespace is required for the Azure Service Bus inbox consumer with Managed Identity.");
		}

		if (string.IsNullOrWhiteSpace(_options.EntityName))
		{
			throw new InvalidOperationException("Runtime:Outbox:EntityName is required for the Azure Service Bus inbox consumer.");
		}

		if (_options.EntityType == RuntimeOutboxEntityType.Topic && string.IsNullOrWhiteSpace(_inboxOptions.Subscription))
		{
			throw new InvalidOperationException(
                "Runtime:Inbox:Subscription is required when the Azure Service Bus outbox entity is a Topic.");
		}

		entityPath = _options.EntityName;
    }

	private ServiceBusClient CreateClient()
    {
        TokenCredential credential;
        if (_options.AuthenticationMode == RuntimeOutboxAuthenticationMode.ManagedIdentity)
        {
            credential = string.IsNullOrWhiteSpace(_options.ManagedIdentityClientId)
                ? new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)
                : new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(_options.ManagedIdentityClientId));
            return new ServiceBusClient(_options.FullyQualifiedNamespace!, credential);
        }
        return new ServiceBusClient(_options.ConnectionString!);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServiceBusProcessor processor;
        lock (_gate)
        {
            _client = CreateClient();
            var options = new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = Math.Max(1, _inboxOptions.MaxConcurrentCalls),
                PrefetchCount = Math.Max(0, _inboxOptions.PrefetchCount)
            };
            if (_inboxOptions.LockRenewalSeconds > 0)
			{
				options.MaxAutoLockRenewalDuration = TimeSpan.FromSeconds(_inboxOptions.LockRenewalSeconds);
			}

			processor = _options.EntityType == RuntimeOutboxEntityType.Topic
                ? _client.CreateProcessor(_options.EntityName, _inboxOptions.Subscription, options)
                : _client.CreateProcessor(_options.EntityName, options);
            _processorHandle = processor;
        }

        processor.ProcessMessageAsync += HandleMessageAsync;
        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception,
                "Azure Service Bus inbox processor error on entity '{Entity}': {Error}",
                args.EntityPath, args.Exception?.Message);
            return Task.CompletedTask;
        };

        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Azure Service Bus inbox consumer starting on '{Entity}'.",
                    _options.EntityType == RuntimeOutboxEntityType.Topic
                        ? $"{_options.EntityName}/{_inboxOptions.Subscription}"
                        : _options.EntityName);
            }
            await processor.StartProcessingAsync(stoppingToken);

            // Keep the host running; Start/StopProcessingAsync manages the actual consumption.
            var stopTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var reg = stoppingToken.Register(() => stopTcs.TrySetResult());
            await stopTcs.Task.ConfigureAwait(false);

            await processor.StopProcessingAsync(CancellationToken.None);
        }
        finally
        {
            processor.ProcessMessageAsync -= HandleMessageAsync;
        }
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;
        InboxEnvelope envelope;
        string messageId;
        try
        {
            envelope = InboxEnvelope.Parse(message.Body.ToArray());
            messageId = !string.IsNullOrWhiteSpace(message.MessageId)
                ? message.MessageId
                : envelope.Id?.ToString("N") ?? Guid.NewGuid().ToString("N");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Service Bus inbox envelope could not be parsed; dead-lettering message {MessageId}.",
                message.MessageId);
            await args.DeadLetterMessageAsync(message, new Dictionary<string, object?>(StringComparer.Ordinal) { ["reason"] = "unparseable_envelope" }, args.CancellationToken);
            return;
        }

        try
        {
            var result = await _processor.ProcessAsync(envelope, messageId, args.CancellationToken);
            await SettleAsync(args, envelope, messageId, result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ex.Message.Contains("OperationCanceled", StringComparison.Ordinal))
        {
            // Abandoning keeps the message redeliverable; wrapping with context lets the
            // processor surface it via ProcessErrorAsync.
            await args.AbandonMessageAsync(message, cancellationToken: args.CancellationToken);
            throw new InvalidOperationException(
                $"Azure Service Bus inbox message {message.MessageId} could not be settled; abandoning and redelivering.",
                ex);
        }
    }

    private async Task SettleAsync(ProcessMessageEventArgs args, InboxEnvelope envelope, string messageId, RuntimeInboxProcessResult result)
    {
        var message = args.Message;
        switch (result.Outcome)
        {
            case RuntimeInboxOutcome.Completed:
                await args.CompleteMessageAsync(message, args.CancellationToken);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Azure Service Bus inbox message {MessageId} ({Event}) processed exactly once.",
                        messageId, envelope.EventType);
                }
                break;
            case RuntimeInboxOutcome.CompletedDuplicate:
                await args.CompleteMessageAsync(message, args.CancellationToken);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Azure Service Bus inbox message {MessageId} ({Event}) already completed; idempotent no-op.",
                        messageId, envelope.EventType);
                }
                break;
            case RuntimeInboxOutcome.Busy:
                // Claim held by another worker and not expired -> release the lock for later redelivery
                // (after claim timeout a later delivery can reclaim it).
                await args.AbandonMessageAsync(message, cancellationToken: args.CancellationToken);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Azure Service Bus inbox message {MessageId} ({Event}) busy (claim held); abandoning.",
                        messageId, envelope.EventType);
                }
                break;
            case RuntimeInboxOutcome.RetryableFailure:
                await args.AbandonMessageAsync(message, cancellationToken: args.CancellationToken);
                _logger.LogWarning("Azure Service Bus inbox message {MessageId} ({Event}) failed transiently; abandoning (redeliver). Reason: {Reason}",
                    messageId, envelope.EventType, result.Reason);
                break;
            case RuntimeInboxOutcome.Rejected:
                await args.DeadLetterMessageAsync(message,
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["reason"] = result.Reason ?? "rejected" },
                    args.CancellationToken);
                _logger.LogError("Azure Service Bus inbox message {MessageId} ({Event}) permanently rejected; dead-lettered. Reason: {Reason}",
                    messageId, envelope.EventType, result.Reason);
                break;
            default:
                await args.AbandonMessageAsync(message, cancellationToken: args.CancellationToken);
                break;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ServiceBusProcessor? processor;
            ServiceBusClient? client;
            lock (_gate)
            {
                processor = _processorHandle;
                client = _client;
                _processorHandle = null;
                _client = null;
            }
            if (processor is not null)
			{
				await processor.DisposeAsync().ConfigureAwait(false);
			}

			if (client is not null)
			{
				await client.DisposeAsync().ConfigureAwait(false);
			}
		}
    }
}
