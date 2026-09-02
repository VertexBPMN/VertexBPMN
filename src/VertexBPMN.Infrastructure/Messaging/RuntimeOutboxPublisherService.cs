using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Operational;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>
/// Leases durable outbox messages and publishes them with at-least-once delivery.
/// Message IDs are stable so downstream consumers can de-duplicate retries.
/// </summary>
public sealed class RuntimeOutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    IRuntimeOutboxTransport transport,
    RuntimeOutboxOptions options,
    ILogger<RuntimeOutboxPublisherService> logger) : BackgroundService
{
    private readonly string _owner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await RunOnceAsync(stoppingToken);
                if (published == 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(options.PollIntervalMilliseconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Runtime outbox publisher cycle failed");
                await Task.Delay(TimeSpan.FromMilliseconds(options.PollIntervalMilliseconds), stoppingToken);
            }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await using var discoveryScope = scopeFactory.CreateAsyncScope();
        var discoveryDb = discoveryScope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var candidateIds = await discoveryDb.RuntimeOutbox
            .AsNoTracking()
            .Where(item =>
                (item.State == "Pending" && (item.LockedUntil == null || item.LockedUntil <= now))
                || (item.State == "InFlight" && item.LockedUntil <= now))
            .OrderBy(item => item.OccurredAt)
            .Select(item => item.Id)
            .Take(Math.Clamp(options.BatchSize, 1, 1_000))
            .ToListAsync(cancellationToken);

        var published = 0;
        foreach (var id in candidateIds)
        {
            var message = await TryLeaseAsync(id, cancellationToken);
            if (message is null)
                continue;

            using var activity = RuntimeTelemetry.ActivitySource.StartActivity(
                "runtime.outbox.publish",
                ActivityKind.Producer);
            activity?.SetTag("messaging.message.id", message.Id);
            activity?.SetTag("messaging.destination.name", options.Destination);
            activity?.SetTag("vertexbpmn.event.type", message.EventType);
            activity?.SetTag("vertexbpmn.process_instance.id", message.ProcessInstanceId);
            activity?.SetTag("vertexbpmn.tenant.id", message.TenantId);

            var started = Stopwatch.GetTimestamp();
            try
            {
                await transport.PublishAsync(message, cancellationToken);
                await MarkPublishedAsync(message.Id, cancellationToken);
                RuntimeTelemetry.OutboxPublished.Add(1,
                    new KeyValuePair<string, object?>("event.type", message.EventType));
                published++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                RuntimeTelemetry.OutboxFailures.Add(1,
                    new KeyValuePair<string, object?>("event.type", message.EventType));
                await MarkFailedAsync(message, ex, cancellationToken);
                logger.LogWarning(ex,
                    "Outbox message {MessageId} failed on attempt {Attempt}",
                    message.Id,
                    message.Attempts);
            }
            finally
            {
                RuntimeTelemetry.OutboxPublishDuration.Record(
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    new KeyValuePair<string, object?>("event.type", message.EventType));
            }
        }

        return published;
    }

    private async Task<RuntimeOutboxMessage?> TryLeaseAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var lockedUntil = now.AddSeconds(Math.Clamp(options.LeaseSeconds, 5, 3_600));
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var changed = await db.RuntimeOutbox
            .Where(item => item.Id == id
                && ((item.State == "Pending" && (item.LockedUntil == null || item.LockedUntil <= now))
                    || (item.State == "InFlight" && item.LockedUntil <= now)))
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, "InFlight")
                    .SetProperty(item => item.LockOwner, _owner)
                    .SetProperty(item => item.LockedUntil, lockedUntil)
                    .SetProperty(item => item.Attempts, item => item.Attempts + 1),
                cancellationToken);
        if (changed != 1)
            return null;

        return await db.RuntimeOutbox.AsNoTracking().SingleAsync(item => item.Id == id, cancellationToken);
    }

    private async Task MarkPublishedAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        await db.RuntimeOutbox
            .Where(item => item.Id == id && item.State == "InFlight" && item.LockOwner == _owner)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, "Published")
                    .SetProperty(item => item.PublishedAt, DateTime.UtcNow)
                    .SetProperty(item => item.LockOwner, (string?)null)
                    .SetProperty(item => item.LockedUntil, (DateTime?)null)
                    .SetProperty(item => item.LastError, (string?)null),
                cancellationToken);
    }

    private async Task MarkFailedAsync(
        RuntimeOutboxMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var deadLetter = message.Attempts >= Math.Max(1, options.MaxAttempts);
        var nextAttempt = deadLetter
            ? (DateTime?)null
            : DateTime.UtcNow.AddSeconds(Math.Clamp(options.RetryDelaySeconds, 0, 86_400));
        var error = exception.ToString();
        if (error.Length > 4_000)
            error = error[..4_000];

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        await db.RuntimeOutbox
            .Where(item => item.Id == message.Id && item.State == "InFlight" && item.LockOwner == _owner)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, deadLetter ? "DeadLetter" : "Pending")
                    .SetProperty(item => item.LockOwner, (string?)null)
                    .SetProperty(item => item.LockedUntil, nextAttempt)
                    .SetProperty(item => item.LastError, error),
                cancellationToken);
    }
}
