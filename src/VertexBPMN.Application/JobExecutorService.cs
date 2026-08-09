using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application;

/// <summary>
/// Background service that polls and executes due jobs (timer, async, etc.).
/// Implements retry & exponential backoff with jitter for failed jobs.
/// </summary>
public class JobExecutorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutorService> _logger;
    private readonly IServiceTaskRegistry _serviceTaskRegistry;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    // Retry/backoff configuration
    private const int DefaultMaxRetries = 5;
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(5);

    // Event type constants (avoid magic strings)
    private static class JobEventTypes
    {
        public const string Executed = "JobExecuted";
        public const string Failed = "JobFailed";
        public const string RetryScheduled = "JobRetryScheduled";
        public const string PermanentlyFailed = "JobPermanentlyFailed";
    }

    public JobExecutorService(
        IServiceProvider serviceProvider,
        ILogger<JobExecutorService> logger,
        IServiceTaskRegistry serviceTaskRegistry)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _serviceTaskRegistry = serviceTaskRegistry;
    }
    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobExecutorService started");
        try
        {
            using var timer = new PeriodicTimer(_pollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunPollingIterationAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in job executor");
        }
        finally
        {
            _logger.LogInformation("JobExecutorService stopped");
        }
    }
    internal async Task RunPollingIterationAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            var eventSink = scope.ServiceProvider.GetService<IProcessMiningEventSink>();


            await foreach (var job in jobRepo.ListDueAsync(DateTime.UtcNow, stoppingToken))
            {
                try
                {
                    _logger.LogInformation("Executing job {JobId} of type {JobType}", job.Id, job.Type);
                    if (!_serviceTaskRegistry.TryResolve(job.Type, out var handler) || handler is null)
                        throw new InvalidOperationException($"No service task handler registered for job type '{job.Type}'.");

                    var payload = ParsePayload(job.Payload);
                    await handler.ExecuteAsync(payload.Attributes, payload.Variables, stoppingToken);
                    await jobRepo.DeleteAsync(job.Id, stoppingToken);

                    if (eventSink != null)
                    {
                        await eventSink.EmitAsync(new ProcessMiningEvent
                        {
                            EventType = JobEventTypes.Executed,
                            ProcessInstanceId = job.ProcessInstanceId.ToString(),
                            TenantId = job.TenantId,
                            Timestamp = DateTimeOffset.UtcNow,
                            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
                            {
                                ["JobId"] = job.Id.ToString(),
                                ["Type"] = job.Type
                            })
                        }, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Job {JobId} failed (attempt {Attempt})", job.Id, job.Retries + 1);

                    var maxRetries = GetMaxRetries(job);
                    var currentAttempt = job.Retries + 1;

                    if (eventSink != null)
                    {
                        await eventSink.EmitAsync(new ProcessMiningEvent
                        {
                            EventType = JobEventTypes.Failed,
                            ProcessInstanceId = job.ProcessInstanceId.ToString(),
                            TenantId = job.TenantId,
                            Timestamp = DateTimeOffset.UtcNow,
                            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
                            {
                                ["JobId"] = job.Id.ToString(),
                                ["Type"] = job.Type,
                                ["Attempt"] = currentAttempt,
                                ["MaxRetries"] = maxRetries,
                                ["ErrorMessage"] = ex.Message
                            })
                        }, stoppingToken);
                    }

                    if (currentAttempt >= maxRetries)
                    {
                        _logger.LogWarning("Job {JobId} reached max retries ({MaxRetries}) and is now permanently failed", job.Id, maxRetries);
                        await jobRepo.DeleteAsync(job.Id, stoppingToken);

                        if (eventSink != null)
                        {
                            await eventSink.EmitAsync(new ProcessMiningEvent
                            {
                                EventType = JobEventTypes.PermanentlyFailed,
                                ProcessInstanceId = job.ProcessInstanceId.ToString(),
                                TenantId = job.TenantId,
                                Timestamp = DateTimeOffset.UtcNow,
                                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
                                {
                                    ["JobId"] = job.Id.ToString(),
                                    ["Type"] = job.Type,
                                    ["Attempts"] = currentAttempt,
                                    ["MaxRetries"] = maxRetries,
                                    ["ErrorMessage"] = ex.ToString()
                                })
                            }, stoppingToken);
                        }
                    }
                    else
                    {
                        var delay = ComputeBackoffDelay(currentAttempt);
                        var nextDue = DateTime.UtcNow + delay;
                        job.Retries = currentAttempt;
                        SetNextDue(job, nextDue);
                        SetErrorMessage(job, ex.Message);
                        await jobRepo.UpdateAsync(job, stoppingToken);

                        _logger.LogInformation("Job {JobId} scheduled for retry {RetryAttempt}/{MaxRetries} at {NextDue} (in {Delay}s)",
                            job.Id, currentAttempt, maxRetries, nextDue, delay.TotalSeconds);

                        if (eventSink != null)
                        {
                            await eventSink.EmitAsync(new ProcessMiningEvent
                            {
                                EventType = JobEventTypes.RetryScheduled,
                                ProcessInstanceId = job.ProcessInstanceId.ToString(),
                                TenantId = job.TenantId,
                                Timestamp = DateTimeOffset.UtcNow,
                                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
                                {
                                    ["JobId"] = job.Id.ToString(),
                                    ["Type"] = job.Type,
                                    ["RetryAttempt"] = currentAttempt,
                                    ["MaxRetries"] = maxRetries,
                                    ["NextDueUtc"] = nextDue,
                                    ["BackoffSeconds"] = delay.TotalSeconds
                                })
                            }, stoppingToken);
                        }
                    }
                }
            }

        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Swallow � iteration canceled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job polling iteration failed");
        }
    }
    // Compute exponential backoff with jitter
    private static TimeSpan ComputeBackoffDelay(int attempt)
    {
        // attempt is 1-based
        var exp = Math.Pow(2, attempt - 1);
        var raw = TimeSpan.FromMilliseconds(BaseRetryDelay.TotalMilliseconds * exp);
        if (raw > MaxRetryDelay) raw = MaxRetryDelay;

        // Jitter factor +/-20%
        var jitterFactor = 1 + (Random.Shared.NextDouble() * 0.4 - 0.2);
        var jittered = TimeSpan.FromMilliseconds(raw.TotalMilliseconds * jitterFactor);
        return jittered < TimeSpan.Zero ? BaseRetryDelay : jittered;
    }

    // Extract max retries with fallback; adjust if domain differs
    private static int GetMaxRetries(dynamic job)
    {
        try
        {
            int value = job.MaxRetries;
            return value > 0 ? value : DefaultMaxRetries;
        }
        catch
        {
            return DefaultMaxRetries;
        }
    }

    private static void SetNextDue(Job job, DateTime dueUtc) => job.DueDate = dueUtc;

    // Set error message if property exists
    private static void SetErrorMessage(dynamic job, string error)
    {
        try { job.ErrorMessage = error; } catch { /* ignore if not present */ }
    }

    private static JobPayload ParsePayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return new JobPayload(new Dictionary<string, string>(), new Dictionary<string, object>());

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var attributes = root.TryGetProperty("attributes", out var attributesElement)
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(attributesElement.GetRawText())
            : null;
        var variables = root.TryGetProperty("variables", out var variablesElement)
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(variablesElement.GetRawText())
            : null;

        return new JobPayload(
            attributes ?? new Dictionary<string, string>(),
            variables ?? new Dictionary<string, object>());
    }

    private sealed record JobPayload(
        Dictionary<string, string> Attributes,
        Dictionary<string, object> Variables);
}
