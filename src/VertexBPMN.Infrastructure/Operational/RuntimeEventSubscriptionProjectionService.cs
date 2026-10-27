using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VertexBPMN.Infrastructure.Operational;

public sealed class RuntimeEventSubscriptionProjectionService(
    IServiceScopeFactory scopeFactory,
    ILogger<RuntimeEventSubscriptionProjectionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Event subscription projection failed; retrying durable batch.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VertexBPMN.Infrastructure.Persistence.BpmnDbContext>();
        var subscriptions = await db.EventStreamSubscriptions.AsNoTracking().ToListAsync(cancellationToken);
        var count = 0;
        foreach (var sub in subscriptions)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // Serialize allocation and commit per subscription across PostgreSQL replicas.
            if (db.Database.IsNpgsql())
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"EventStreamSubscriptions\" WHERE \"Id\" = {sub.Id} FOR UPDATE", cancellationToken);
            }
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var expiredThrough = await db.EventStreamDeliveries
                .Where(x => x.SubscriptionId == sub.Id && x.RetainedAt < cutoff)
                .Select(x => (long?)x.Id).MaxAsync(cancellationToken) ?? 0;
            if (expiredThrough > 0)
            {
                await db.EventStreamSubscriptions.Where(x => x.Id == sub.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiredThrough,
                        x => Math.Max(x.ExpiredThrough, expiredThrough)), cancellationToken);
                await db.EventStreamDeliveries.Where(x => x.SubscriptionId == sub.Id && x.Id <= expiredThrough)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Payload, string.Empty), cancellationToken);
            }
            var messages = await db.RuntimeOutbox.AsNoTracking()
                .Where(x => x.TenantId == sub.TenantId && x.State == "Published" && x.PublishedAt != null
                    && !db.EventStreamDeliveries.Any(d => d.SubscriptionId == sub.Id && d.SourceEventId == x.Id))
                .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Take(100).ToListAsync(cancellationToken);
            foreach (var message in messages)
            {
                db.EventStreamDeliveries.Add(new VertexBPMN.Domain.Entities.EventStreamDelivery
                {
                    SubscriptionId = sub.Id, SourceEventId = message.Id, TenantId = sub.TenantId,
                    EventType = message.EventType, Payload = message.Payload,
                    DefinitionId = await db.ProcessInstances.Where(i => i.Id == message.ProcessInstanceId
                        && i.TenantId == sub.TenantId && i.ProcessDefinition.TenantId == sub.TenantId)
                        .Select(i => (Guid?)i.ProcessDefinitionId).SingleOrDefaultAsync(cancellationToken)
                });
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            count += messages.Count;
        }
        return count;
    }
}
