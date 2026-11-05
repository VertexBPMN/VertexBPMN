using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Operational;

namespace VertexBPMN.Tests.Integration.Api;

public sealed class Lc09EventSubscriptionOrderingTests
{
    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task EqualTimestamps_AndLatePublisherCompletion_ReplayWithoutGaps()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(ct);
        var tenant = "order-" + Guid.NewGuid().ToString("N");
        var sub = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "ordering" };
        var timestamp = DateTime.UtcNow;
        var messages = Enumerable.Range(0, 205).Select(_ => new RuntimeOutboxMessage
        {
            Id = Guid.NewGuid(), ProcessInstanceId = Guid.NewGuid(), TenantId = tenant,
            State = "Published", PublishedAt = timestamp, OccurredAt = timestamp, EventType = "ORDER"
        }).ToArray();
        db.EventStreamSubscriptions.Add(sub);
        db.RuntimeOutbox.AddRange(messages);
        await db.SaveChangesAsync(ct);
        await using var one = await Lc09ContractHost.StartAsync(tenant);
        await using var two = await Lc09ContractHost.StartAsync(tenant);
        var first = one.Services.GetRequiredService<RuntimeEventSubscriptionProjectionService>();
        var second = two.Services.GetRequiredService<RuntimeEventSubscriptionProjectionService>();
        await Task.WhenAll(first.RunOnceAsync(ct), second.RunOnceAsync(ct));
        await first.RunOnceAsync(ct);
        using var http = one.GetTestClient();
        var replay = await ReadAllAsync(http, sub.Id, ct);
        Assert.Equal(205, replay.Count);
        Assert.Equal(205, replay.Select(x => x.SourceEventId).Distinct().Count());
        Assert.Equal(replay.Select(x => x.Id).Order(), replay.Select(x => x.Id));
        var cursor = EventStreamCursor.Encode(sub.Id, replay[^1].Id);
        var late = new RuntimeOutboxMessage
        {
            Id = Guid.NewGuid(), ProcessInstanceId = Guid.NewGuid(), TenantId = tenant,
            State = "Published", PublishedAt = DateTime.UtcNow, OccurredAt = timestamp.AddDays(-1), EventType = "LATE"
        };
        db.RuntimeOutbox.Add(late);
        await db.SaveChangesAsync(ct);
        await Task.WhenAll(first.RunOnceAsync(ct), second.RunOnceAsync(ct));
        var tail = await http.GetFromJsonAsync<EventSubscriptionController.EventStreamPage>(
            $"/api/event-subscriptions/{sub.Id}/events?after={cursor}", ct);
        Assert.NotNull(tail);
        Assert.Equal(late.Id, Assert.Single(tail.Events).SourceEventId);
        Assert.Equal(replay.Select(x => x.SourceEventId).Append(late.Id),
            (await ReadAllAsync(http, sub.Id, ct)).Select(x => x.SourceEventId));
        await one.StopAsync(ct);
        await two.StopAsync(ct);
    }

    private static async Task<List<EventStreamDelivery>> ReadAllAsync(HttpClient http, Guid sub, CancellationToken ct)
    {
        var result = new List<EventStreamDelivery>();
        string? cursor = null;
        for (var pageNumber = 0; pageNumber < 10; pageNumber++)
        {
            var path = $"/api/event-subscriptions/{sub}/events" + (cursor is null ? "" : "?after=" + cursor);
            var page = await http.GetFromJsonAsync<EventSubscriptionController.EventStreamPage>(path, ct);
            Assert.NotNull(page);
            if (page.Events.Count == 0)
            {
                return result;
            }
            result.AddRange(page.Events);
            cursor = page.NextCursor;
        }
        throw new InvalidOperationException("Pagination did not terminate.");
    }
}
