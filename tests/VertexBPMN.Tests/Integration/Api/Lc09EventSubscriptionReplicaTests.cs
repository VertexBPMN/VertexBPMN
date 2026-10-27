using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Operational;

namespace VertexBPMN.Tests.Integration.Api;

public sealed class Lc09EventSubscriptionReplicaTests
{
    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task ReplicaProjection_WaitsForSubscriptionCommitBoundary()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(ct);
        var tenant = "race-" + Guid.NewGuid().ToString("N");
        var sub = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "replica" };
        db.EventStreamSubscriptions.Add(sub);
        db.RuntimeOutbox.Add(new RuntimeOutboxMessage
        {
            Id = Guid.NewGuid(), ProcessInstanceId = Guid.NewGuid(), TenantId = tenant,
            State = "Published", PublishedAt = DateTime.UtcNow, EventType = "RACE"
        });
        await db.SaveChangesAsync(ct);
        await using var first = await Lc09ContractHost.StartAsync(tenant);
        await using var second = await Lc09ContractHost.StartAsync(tenant);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"EventStreamSubscriptions\" WHERE \"Id\" = {sub.Id} FOR UPDATE", ct);
        var one = first.Services.GetRequiredService<RuntimeEventSubscriptionProjectionService>().RunOnceAsync(ct);
        var two = second.Services.GetRequiredService<RuntimeEventSubscriptionProjectionService>().RunOnceAsync(ct);
        await Task.Delay(500, ct);
        var completed = one.IsCompleted || two.IsCompleted;
        await transaction.CommitAsync(ct);
        var failure = await Record.ExceptionAsync(async () => await Task.WhenAll(one, two));
        Assert.Null(failure);
        Assert.False(completed, "A replica must wait for the subscription transaction before allocating read positions.");
        Assert.Single(await db.EventStreamDeliveries.Where(x => x.SubscriptionId == sub.Id).ToListAsync(ct));
        await first.StopAsync(ct);
        await second.StopAsync(ct);
    }
}
