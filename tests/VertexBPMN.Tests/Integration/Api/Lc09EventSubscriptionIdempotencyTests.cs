using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Operational;

namespace VertexBPMN.Tests.Integration.Api;

// Independent business-effect safety: duplicate delivery / re-projection of the SAME
// source event must not create a second durable delivery and must not re-trigger a
// subscriber business effect keyed on SourceEventId.
public sealed class Lc09EventSubscriptionIdempotencyTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RuntimeOutboxMessage Message(string tenant, Guid id) => new()
    {
        Id = id, ProcessInstanceId = Guid.NewGuid(), TenantId = tenant,
        EventType = "EFFECT", Payload = "{\"effect\":1}", State = "Pending", OccurredAt = DateTime.UtcNow
    };

    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task ReProjection_OfSamePublishedEvent_DoesNotDuplicateDelivery()
    {
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(Ct);
        var tenant = "idem-" + Guid.NewGuid().ToString("N");
        var sub = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "idem" };
        var source = Message(tenant, Guid.NewGuid());
        var foreign = Message(tenant + "-foreign", Guid.NewGuid());
        db.EventStreamSubscriptions.Add(sub);
        db.RuntimeOutbox.AddRange(source, foreign);
        await db.SaveChangesAsync(Ct);

        await using var first = await Lc09ContractHost.StartAsync(tenant);
        await Lc09PublishProbe.PublishAsync(first.Services);
        var projection = first.Services.GetRequiredService<RuntimeEventSubscriptionProjectionService>();
        Assert.Equal(1, await projection.RunOnceAsync(Ct));

        // Subscriber crash + retry: the same Published source event is reprocessed.
        Assert.Equal(0, await projection.RunOnceAsync(Ct));
        Assert.Equal(0, await projection.RunOnceAsync(Ct));

        var deliveries = await db.EventStreamDeliveries.Where(x => x.SubscriptionId == sub.Id).ToListAsync(Ct);
        // One durable delivery, one independent business-effect opportunity only.
        Assert.Equal(source.Id, Assert.Single(deliveries).SourceEventId);
        await first.StopAsync(Ct);
    }

    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task DuplicateRead_AtSameCursor_ReturnsIdenticalEvent_WithoutStoreMutation()
    {
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(Ct);
        var tenant = "idem-" + Guid.NewGuid().ToString("N");
        var sub = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "idem" };
        var source = Message(tenant, Guid.NewGuid());
        var foreign = Message(tenant + "-foreign", Guid.NewGuid());
        db.EventStreamSubscriptions.Add(sub);
        db.RuntimeOutbox.AddRange(source, foreign);
        await db.SaveChangesAsync(Ct);

        await using var host = await Lc09ContractHost.StartAsync(tenant);
        await Lc09PublishProbe.PublishAsync(host.Services);
        await host.Services.GetRequiredService<RuntimeEventSubscriptionProjectionService>().RunOnceAsync(Ct);
        using var http = host.GetTestClient();

        var first = await http.GetFromJsonAsync<EventSubscriptionController.EventStreamPage>(
            $"/api/event-subscriptions/{sub.Id}/events", Ct);
        Assert.NotNull(first);
        var firstEvent = Assert.Single(first.Events);
        Assert.NotNull(first.NextCursor);

        // A retry/duplicate read (no ack, same starting point) returns the identical
        // projected event again and does not create a second durable row.
        var dup = await http.GetFromJsonAsync<EventSubscriptionController.EventStreamPage>(
            $"/api/event-subscriptions/{sub.Id}/events", Ct);
        Assert.NotNull(dup);
        var again = Assert.Single(dup.Events);
        Assert.Equal(firstEvent.Id, again.Id);
        Assert.Equal(firstEvent.SourceEventId, again.SourceEventId);
        Assert.Single(await db.EventStreamDeliveries.Where(x => x.SubscriptionId == sub.Id && x.SourceEventId == source.Id).ToListAsync(Ct));
        await host.StopAsync(Ct);
    }
}
