using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Infrastructure.Operational;

public sealed class RuntimeOutboxAnalyticsProjectionService(
    IServiceScopeFactory scopeFactory,
    ILogger<RuntimeOutboxAnalyticsProjectionService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var projected = await ProjectBatchAsync(100, stoppingToken);
                if (projected == 0)
                    await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Runtime analytics projection failed; the batch will be retried.");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    public async Task<int> ProjectBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));

        await using var discoveryScope = scopeFactory.CreateAsyncScope();
        var discoveryDb = discoveryScope.ServiceProvider.GetRequiredService<BpmnDbContext>();
        var candidates = await discoveryDb.RuntimeOutbox.AsNoTracking()
            .Where(message => message.AnalyticsProjectedAt == null)
            .OrderBy(message => message.OccurredAt)
            .ThenBy(message => message.Id)
            .Take(batchSize)
            .Select(message => new ProjectionCandidate(
                message.Id,
                message.ProcessInstanceId,
                message.EventType,
                message.Payload,
                message.TenantId,
                message.OccurredAt))
            .ToArrayAsync(cancellationToken);

        var projected = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var scope = scopeFactory.CreateAsyncScope();
            var miningDb = scope.ServiceProvider.GetRequiredService<ProcessMiningEventDbContext>();
            if (!await miningDb.Events.AnyAsync(
                    item => item.SourceEventId == candidate.Id, cancellationToken))
            {
                miningDb.Events.Add(ToMiningEvent(candidate));
                try
                {
                    await miningDb.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    miningDb.ChangeTracker.Clear();
                    if (!await miningDb.Events.AnyAsync(
                            item => item.SourceEventId == candidate.Id, cancellationToken))
                        throw;
                }
            }

            var runtimeDb = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
            projected += await runtimeDb.RuntimeOutbox
                .Where(message => message.Id == candidate.Id && message.AnalyticsProjectedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(message => message.AnalyticsProjectedAt, DateTime.UtcNow), cancellationToken);
        }

        return projected;
    }

    private static ProcessMiningEvent ToMiningEvent(ProjectionCandidate candidate)
    {
        string? taskId = null;
        string? activityId = null;
        string? userId = null;
        try
        {
            using var payload = JsonDocument.Parse(candidate.Payload);
            if (payload.RootElement.ValueKind == JsonValueKind.Object)
            {
                taskId = PropertyText(payload.RootElement, "taskId", "Id");
                activityId = PropertyText(payload.RootElement, "activityId");
                userId = PropertyText(payload.RootElement, "userId");
            }
        }
        catch (JsonException)
        {
            // Keep the original payload for diagnostics; malformed optional metadata
            // must not prevent projection of the durable runtime event itself.
        }

        return new ProcessMiningEvent
        {
            SourceEventId = candidate.Id,
            EventType = candidate.EventType,
            ProcessInstanceId = candidate.ProcessInstanceId.ToString(),
            TaskId = taskId,
            ActivityId = activityId,
            UserId = userId,
            TenantId = candidate.TenantId,
            Timestamp = new DateTimeOffset(DateTime.SpecifyKind(candidate.OccurredAt, DateTimeKind.Utc)),
            PayloadJson = candidate.Payload
        };
    }

    private static string? PropertyText(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
            if (names.Any(name => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)))
                return property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.ToString();
        return null;
    }

    private sealed record ProjectionCandidate(
        Guid Id,
        Guid ProcessInstanceId,
        string EventType,
        string Payload,
        string? TenantId,
        DateTime OccurredAt);
}
