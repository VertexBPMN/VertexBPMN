using System.Net;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Operational;

namespace VertexBPMN.Tests.Integration.Api;

public sealed class Lc09EventSubscriptionRetentionTests
{
    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task ExpiredHistory_IsExplicitAndCannotBeReprojected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(ct);
        var tenant = "ttl-" + Guid.NewGuid().ToString("N");
        var sub = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "ttl" };
        var old = new RuntimeOutboxMessage
        {
            Id = Guid.NewGuid(), ProcessInstanceId = Guid.NewGuid(), TenantId = tenant,
            State = "Published", PublishedAt = DateTime.UtcNow.AddDays(-8), EventType = "OLD"
        };
        db.EventStreamSubscriptions.Add(sub);
        db.RuntimeOutbox.Add(old);
        db.EventStreamDeliveries.Add(new EventStreamDelivery
        {
            SubscriptionId = sub.Id, TenantId = tenant, SourceEventId = old.Id, EventType = "OLD",
            RetainedAt = DateTime.UtcNow.AddDays(-8)
        });
        await db.SaveChangesAsync(ct);
        await using var host = await Lc09ContractHost.StartAsync(tenant);
        var service = host.Services.GetRequiredService<RuntimeEventSubscriptionProjectionService>();
        await service.RunOnceAsync(ct);
        await service.RunOnceAsync(ct);
        using var http = host.GetTestClient();
        using var response = await http.GetAsync($"/api/event-subscriptions/{sub.Id}/events", ct);
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Contains("event_cursor_expired", await response.Content.ReadAsStringAsync(ct), StringComparison.Ordinal);
        using var expiredCursor = await http.GetAsync($"/api/event-subscriptions/{sub.Id}/events?after="
            + EventStreamCursor.Encode(sub.Id, 1), ct);
        // Only a cursor strictly before the expired prefix is stale; its exact end resumes safely.
        var floor = await db.EventStreamSubscriptions.Where(x => x.Id == sub.Id).Select(x => x.ExpiredThrough).SingleAsync(ct);
        Assert.Equal(floor > 1 ? HttpStatusCode.Gone : HttpStatusCode.OK, expiredCursor.StatusCode);
        Assert.Single(await db.EventStreamDeliveries.Where(x => x.SubscriptionId == sub.Id).ToListAsync(ct));
        Assert.All(await db.EventStreamDeliveries.Where(x => x.SubscriptionId == sub.Id).ToListAsync(ct),
            row => Assert.Empty(row.Payload));
        await host.StopAsync(ct);
    }
}
