using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.AgentWorker;

public sealed class ExternalTaskWorkerService(
    IExternalTaskApiClient api,
    IEnumerable<IExternalTaskHandler> handlers,
    IOptions<ExternalTaskWorkerOptions> options,
    ILogger<ExternalTaskWorkerService> logger,
    TimeProvider? clock = null) : BackgroundService
{
    private readonly ConcurrentDictionary<Guid, Task> _active = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        var byTopic = handlers.GroupBy(handler => handler.Topic, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var topics = settings.Topics.Where(byTopic.ContainsKey).ToArray();
        if (topics.Length != settings.Topics.Length)
            throw new InvalidOperationException("Every configured external task topic requires exactly one handler.");

        var failures = 0;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var completed in _active.Where(item => item.Value.IsCompleted).ToArray())
                {
                    _active.TryRemove(completed.Key, out _);
                    try { await completed.Value; }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                    catch (Exception exception) { logger.LogError(exception, "External task execution ended unexpectedly."); }
                }

                var capacity = settings.MaxConcurrency - _active.Count;
                if (capacity <= 0)
                {
                    await Task.Delay(settings.PollMilliseconds, stoppingToken);
                    continue;
                }

                try
                {
                    var claimed = await api.ClaimAsync(topics, Math.Min(capacity, settings.MaxTasksPerClaim),
                        settings.LeaseSeconds, stoppingToken);
                    failures = 0;
                    foreach (var lease in claimed)
                    {
                        if (!byTopic.TryGetValue(lease.Topic, out var handler)) continue;
                        _active.TryAdd(lease.JobId, RunLeaseAsync(lease, handler, settings, stoppingToken));
                    }
                    if (claimed.Count == 0) await Task.Delay(settings.PollMilliseconds, stoppingToken);
                }
                catch (ExternalTaskTransportException exception)
                {
                    failures++;
                    var delay = exception.RetryAfter ?? Backoff(failures, settings.MaximumBackoffSeconds);
                    logger.LogWarning("External task API unavailable; polling resumes after {Delay}.", delay);
                    await Task.Delay(delay, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            try
            {
                await Task.WhenAll(_active.Values)
                    .WaitAsync(TimeSpan.FromSeconds(settings.LeaseSeconds), CancellationToken.None);
            }
            catch (TimeoutException)
            {
                logger.LogWarning("External task handlers exceeded the graceful-shutdown window.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
    }

    private async Task RunLeaseAsync(ExternalTaskLease lease, IExternalTaskHandler handler,
        ExternalTaskWorkerOptions settings, CancellationToken shutdown)
    {
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
        var leaseState = new LeaseState(lease.LeaseExpiresAt);
        var work = handler.HandleAsync(lease, execution.Token).AsTask();
        var heartbeat = KeepLeaseAsync(lease, settings, execution, leaseState, shutdown);
        var first = await Task.WhenAny(work, heartbeat);
        if (first == heartbeat && !await heartbeat) execution.Cancel();
        if (first == work) execution.Cancel();
        ExternalTaskHandlerResult? result = null;
        try { result = await work; }
        catch (OperationCanceledException) when (execution.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "External task handler failed; reporting a redacted technical failure.");
            result = ExternalTaskHandlerResult.TechnicalFailure("transport_failure");
        }
        var leaseValid = await heartbeat;
        if (result is null || !leaseValid || shutdown.IsCancellationRequested) return;
        await SubmitResultAsync(lease, result, leaseState, settings, shutdown);
    }

    private async Task<bool> KeepLeaseAsync(ExternalTaskLease lease, ExternalTaskWorkerOptions settings,
        CancellationTokenSource execution, LeaseState leaseState, CancellationToken shutdown)
    {
        var failures = 0;
        var leaseExpiresAt = lease.LeaseExpiresAt;
        while (!execution.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(settings.HeartbeatSeconds), execution.Token); }
            catch (OperationCanceledException) { return true; }
            try
            {
                ExternalTaskWorkerHeartbeatResult heartbeat;
                try { heartbeat = await api.HeartbeatAsync(lease, settings.LeaseSeconds, execution.Token); }
                catch (OperationCanceledException) when (execution.IsCancellationRequested) { return true; }
                if (heartbeat.Outcome == ExternalTaskHeartbeatOutcome.LeaseLost) return false;
                if (heartbeat.LeaseExpiresAt is null || heartbeat.LeaseExpiresAt <= leaseExpiresAt) return false;
                leaseExpiresAt = heartbeat.LeaseExpiresAt.Value;
                leaseState.ExpiresAt = leaseExpiresAt;
                failures = 0;
            }
            catch (ExternalTaskTransportException exception)
            {
                failures++;
                var now = (clock ?? TimeProvider.System).GetUtcNow().ToUnixTimeMilliseconds();
                var delay = exception.RetryAfter ?? Backoff(failures, Math.Min(settings.HeartbeatSeconds, 5));
                if (now + delay.TotalMilliseconds >= leaseExpiresAt) return false;
                try { await Task.Delay(delay, execution.Token); }
                catch (OperationCanceledException) { return true; }
            }
        }
        return true;
    }

    private async Task SubmitResultAsync(ExternalTaskLease lease, ExternalTaskHandlerResult result,
        LeaseState leaseState, ExternalTaskWorkerOptions settings, CancellationToken shutdown)
    {
        if (result.Outcome == ExternalTaskHandlerOutcome.Success && result.Result.ValueKind == JsonValueKind.Undefined)
            throw new InvalidOperationException("A successful external task result requires JSON output.");
        if (result.Outcome != ExternalTaskHandlerOutcome.Success && string.IsNullOrWhiteSpace(result.Code))
            throw new InvalidOperationException("A failed external task result requires a stable error code.");
        var receiptId = Guid.NewGuid();
        var failures = 0;
        while (!shutdown.IsCancellationRequested)
        {
            try
            {
                var mutation = result.Outcome switch
                {
                    ExternalTaskHandlerOutcome.Success => await api.CompleteAsync(
                        lease, receiptId, result.Result, shutdown),
                    ExternalTaskHandlerOutcome.BusinessError => await api.FailAsync(
                        lease, receiptId, "business", result.Code!, shutdown),
                    _ => await api.FailAsync(lease, receiptId, "technical", result.Code!, shutdown)
                };
                if (mutation.Outcome == ExternalTaskMutationOutcome.Rejected)
                    logger.LogError("External task result was rejected by the server for job {JobId}.", lease.JobId);
                return;
            }
            catch (ExternalTaskTransportException exception)
            {
                failures++;
                var delay = exception.RetryAfter ?? Backoff(failures, Math.Min(settings.MaximumBackoffSeconds, 5));
                var now = (clock ?? TimeProvider.System).GetUtcNow().ToUnixTimeMilliseconds();
                if (now + delay.TotalMilliseconds >= leaseState.ExpiresAt) return;
                await Task.Delay(delay, shutdown);
            }
        }
    }

    private sealed class LeaseState(long expiresAt)
    {
        public long ExpiresAt { get; set; } = expiresAt;
    }

    private static TimeSpan Backoff(int failures, int maximumSeconds)
    {
        var exponent = Math.Min(6, Math.Max(0, failures - 1));
        var seconds = Math.Min(maximumSeconds, 1 << exponent);
        return TimeSpan.FromMilliseconds(seconds * 1000 + Random.Shared.Next(0, 251));
    }
}
