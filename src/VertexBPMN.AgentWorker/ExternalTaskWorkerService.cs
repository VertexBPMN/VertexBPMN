using System.Collections.Concurrent;
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
        var work = handler.HandleAsync(lease, execution.Token).AsTask();
        var heartbeat = KeepLeaseAsync(lease, settings, execution, shutdown);
        var first = await Task.WhenAny(work, heartbeat);
        if (first == heartbeat && !await heartbeat) execution.Cancel();
        if (first == work) execution.Cancel();
        try { await work; }
        catch (OperationCanceledException) when (execution.IsCancellationRequested) { }
        if (!heartbeat.IsCompleted) await heartbeat;
    }

    private async Task<bool> KeepLeaseAsync(ExternalTaskLease lease, ExternalTaskWorkerOptions settings,
        CancellationTokenSource execution, CancellationToken shutdown)
    {
        var failures = 0;
        var leaseExpiresAt = lease.LeaseExpiresAt;
        while (!execution.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(settings.HeartbeatSeconds), execution.Token); }
            catch (OperationCanceledException) { return true; }
            try
            {
                var heartbeat = await api.HeartbeatAsync(lease, settings.LeaseSeconds, shutdown);
                if (heartbeat.Outcome == ExternalTaskHeartbeatOutcome.LeaseLost) return false;
                if (heartbeat.LeaseExpiresAt is null || heartbeat.LeaseExpiresAt <= leaseExpiresAt) return false;
                leaseExpiresAt = heartbeat.LeaseExpiresAt.Value;
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

    private static TimeSpan Backoff(int failures, int maximumSeconds)
    {
        var exponent = Math.Min(6, Math.Max(0, failures - 1));
        var seconds = Math.Min(maximumSeconds, 1 << exponent);
        return TimeSpan.FromMilliseconds(seconds * 1000 + Random.Shared.Next(0, 251));
    }
}
