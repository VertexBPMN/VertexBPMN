using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Tests.Integration.Api;

// HTTP management contract (create/list/delete) with role policies; test principal only.
public sealed class Lc09EventSubscriptionManagementTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record CreateBody(string? Name = null);

    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task Create_ReturnsTenantOwnedSubscription()
    {
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(Ct);
        var tenant = "mgr-" + Guid.NewGuid().ToString("N");
        await using var host = await Lc09ContractHost.StartAsync(tenant, ["Admin"]);
        using var http = host.GetTestClient();
        var response = await http.PostAsJsonAsync("/api/event-subscriptions", new CreateBody("orders"), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EventStreamSubscription>(Ct);
        Assert.NotNull(created);
        Assert.Equal(tenant, created.TenantId);
        var row = await db.EventStreamSubscriptions.SingleAsync(x => x.Id == created.Id, Ct);
        Assert.Equal(tenant, row.TenantId);
        Assert.Equal("orders", row.Name);
        await host.StopAsync(Ct);
    }

    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task Create_RequiresManagerRole()
    {
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(Ct);
        var tenant = "mgr-" + Guid.NewGuid().ToString("N");
        await using var host = await Lc09ContractHost.StartAsync(tenant, ["ReadOnly"]);
        using var http = host.GetTestClient();
        var response = await http.PostAsJsonAsync("/api/event-subscriptions", new CreateBody("orders"), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await db.EventStreamSubscriptions.Where(x => x.TenantId == tenant).ToListAsync(Ct));
        await host.StopAsync(Ct);
    }

    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task List_ReturnsOnlyOwnSubscriptions()
    {
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(Ct);
        var tenant = "mgr-" + Guid.NewGuid().ToString("N");
        var other = tenant + "-other";
        var mine = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "a" };
        var theirs = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = other, Name = "b" };
        db.EventStreamSubscriptions.AddRange(mine, theirs);
        await db.SaveChangesAsync(Ct);
        await using var host = await Lc09ContractHost.StartAsync(tenant, ["ReadOnly"]);
        using var http = host.GetTestClient();
        var list = await http.GetFromJsonAsync<List<EventStreamSubscription>>("/api/event-subscriptions", Ct);
        Assert.NotNull(list);
        Assert.Equal(mine.Id, Assert.Single(list).Id);
        await host.StopAsync(Ct);
    }

    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task Delete_RemovesOwnSubscriptionAndDeliveries()
    {
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(Ct);
        var tenant = "mgr-" + Guid.NewGuid().ToString("N");
        var sub = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "x" };
        db.EventStreamSubscriptions.Add(sub);
        db.EventStreamDeliveries.Add(new EventStreamDelivery
        {
            SubscriptionId = sub.Id, TenantId = tenant, SourceEventId = Guid.NewGuid(),
            EventType = "T", Payload = "{}"
        });
        await db.SaveChangesAsync(Ct);
        await using var host = await Lc09ContractHost.StartAsync(tenant, ["Admin"]);
        using var http = host.GetTestClient();
        var response = await http.DeleteAsync($"/api/event-subscriptions/{sub.Id}", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await db.EventStreamSubscriptions.AnyAsync(x => x.Id == sub.Id, Ct));
        Assert.Empty(await db.EventStreamDeliveries.Where(x => x.SubscriptionId == sub.Id).ToListAsync(Ct));
        await host.StopAsync(Ct);
    }

    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task Delete_ForeignSubscription_ReturnsNotFound()
    {
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(Ct);
        var tenant = "mgr-" + Guid.NewGuid().ToString("N");
        var foreign = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant + "-other", Name = "x" };
        db.EventStreamSubscriptions.Add(foreign);
        await db.SaveChangesAsync(Ct);
        await using var host = await Lc09ContractHost.StartAsync(tenant, ["Admin"]);
        using var http = host.GetTestClient();
        var response = await http.DeleteAsync($"/api/event-subscriptions/{foreign.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(await db.EventStreamSubscriptions.AnyAsync(x => x.Id == foreign.Id, Ct));
        await host.StopAsync(Ct);
    }

    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task Management_DisabledWhenFeatureOff()
    {
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(Ct);
        var tenant = "mgr-" + Guid.NewGuid().ToString("N");
        await using var host = await Lc09ContractHost.StartAsync(tenant, ["Admin"], featureOn: false);
        using var http = host.GetTestClient();
        var sub = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "x" };
        db.EventStreamSubscriptions.Add(sub);
        await db.SaveChangesAsync(Ct);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsJsonAsync("/api/event-subscriptions", new CreateBody("n"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync("/api/event-subscriptions", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.DeleteAsync($"/api/event-subscriptions/{sub.Id}", Ct)).StatusCode);
        await host.StopAsync(Ct);
    }
}
