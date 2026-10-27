using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Tests.Integration.Api;

public sealed class Lc09EventSubscriptionReadBoundaryTests
{
    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task Read_WaitsForAtomicRetentionBoundary()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(ct);
        var tenant = "read-lock-" + Guid.NewGuid().ToString("N");
        var sub = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "read" };
        db.EventStreamSubscriptions.Add(sub);
        await db.SaveChangesAsync(ct);
        await using var host = await Lc09ContractHost.StartAsync(tenant);
        using var http = host.GetTestClient();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"EventStreamSubscriptions\" WHERE \"Id\" = {sub.Id} FOR UPDATE", ct);
        var read = http.GetAsync($"/api/event-subscriptions/{sub.Id}/events", ct);
        await Task.Delay(2000, ct);
        var completed = read.IsCompleted;
        await transaction.CommitAsync(ct);
        using var response = await read;
        Assert.False(completed, "Read must not race between retention floor and payload query.");
        response.EnsureSuccessStatusCode();
        await host.StopAsync(ct);
    }
}
