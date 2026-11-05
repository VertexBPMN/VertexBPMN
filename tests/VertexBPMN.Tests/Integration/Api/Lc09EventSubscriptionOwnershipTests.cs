using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Operational;

namespace VertexBPMN.Tests.Integration.Api;

// Security contract with test principal; not authentication acceptance.
public sealed class Lc09EventSubscriptionOwnershipTests
{
    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task ForeignTenant_AndCrossSubscriptionCursor_AreRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(ct);
        var tenant = "owner-" + Guid.NewGuid().ToString("N");
        var own = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "own" };
        var other = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant + "-other", Name = "foreign" };
        db.EventStreamSubscriptions.AddRange(own, other);
        await db.SaveChangesAsync(ct);
        await using var host = await Lc09ContractHost.StartAsync(tenant);
        using var http = host.GetTestClient();
        var path = $"/api/event-subscriptions/{own.Id}/events";
        using var foreign = await http.GetAsync($"/api/event-subscriptions/{other.Id}/events?tenantId={other.TenantId}", ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var crossed = await http.GetAsync(path + "?after=" + EventStreamCursor.Encode(other.Id, 1), ct);
        Assert.Equal(HttpStatusCode.BadRequest, crossed.StatusCode);
        foreach (var invalid in new[] { "v1:bad:1", $"v1:{own.Id:N}:-1", $"v1:{own.Id:N}:9223372036854775808" })
        {
            using var response = await http.GetAsync(path + "?after=" + invalid, ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.False(await db.EventStreamDeliveries.AnyAsync(x => x.SubscriptionId == own.Id, ct));
        await host.StopAsync(ct);
    }
}
