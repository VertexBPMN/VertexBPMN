using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Operational;

namespace VertexBPMN.Tests.Integration.Api;

// PostgreSQL + real outbox publisher/broker + HTTP contract; test principal only.
public sealed class Lc09EventSubscriptionCheckpointTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task PublishedEvent_RemainsReadableAfterServiceRecreation()
    {
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(Ct);
        Assert.Contains("20261026225112_EventStreamReadProjection", await db.Database.GetAppliedMigrationsAsync(Ct), StringComparer.Ordinal);
        var tenant = "lc09-" + Guid.NewGuid().ToString("N");
        var sub = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "checkpoint" };
        var own = Message(tenant);
        var foreign = Message(tenant + "-foreign");
        db.EventStreamSubscriptions.Add(sub);
        db.RuntimeOutbox.AddRange(own, foreign);
        await db.SaveChangesAsync(Ct);
        await using (var first = await Lc09ContractHost.StartAsync(tenant))
        {
            await Lc09PublishProbe.PublishAsync(first.Services);
            await first.Services.GetRequiredService<RuntimeEventSubscriptionProjectionService>().RunOnceAsync(Ct);
            using var http = first.GetTestClient();
            var page = await http.GetFromJsonAsync<EventSubscriptionController.EventStreamPage>($"/api/event-subscriptions/{sub.Id}/events", Ct);
            Assert.NotNull(page);
            Assert.Equal(own.Id, Assert.Single(page.Events).SourceEventId);
            Assert.NotNull(page.NextCursor);
            await first.StopAsync(Ct);
        }
        await using var second = await Lc09ContractHost.StartAsync(tenant);
        using var secondHttp = second.GetTestClient();
        var again = await secondHttp.GetFromJsonAsync<EventSubscriptionController.EventStreamPage>($"/api/event-subscriptions/{sub.Id}/events", Ct);
        Assert.NotNull(again);
        Assert.Equal(own.Id, Assert.Single(again.Events).SourceEventId);
        Assert.Equal(own.Payload, again.Events[0].Payload);
        var tail = await secondHttp.GetFromJsonAsync<EventSubscriptionController.EventStreamPage>(
            $"/api/event-subscriptions/{sub.Id}/events?after={again.NextCursor}", Ct);
        Assert.NotNull(tail);
        Assert.Empty(tail.Events);
        Assert.Single(await db.EventStreamDeliveries.Where(x => x.SubscriptionId == sub.Id).ToListAsync(Ct));
        await second.StopAsync(Ct);
    }

    private static RuntimeOutboxMessage Message(string tenant) => new()
    {
        Id = Guid.NewGuid(), ProcessInstanceId = Guid.NewGuid(), TenantId = tenant,
        EventType = "CHECKPOINT", Payload = "{\"value\":42}", State = "Pending", OccurredAt = DateTime.UtcNow
    };
}
