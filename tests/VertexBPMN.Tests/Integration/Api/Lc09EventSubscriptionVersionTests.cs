using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Operational;

namespace VertexBPMN.Tests.Integration.Api;

public sealed class Lc09EventSubscriptionVersionTests
{
    [Fact]
    [Trait("Category", "Lc09PostgresContract")]
    public async Task DefinitionFilter_UsesPersistedInstanceVersionBinding()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = Lc09ContractHost.Database();
        await db.Database.MigrateAsync(ct);
        var tenant = "ver-" + Guid.NewGuid().ToString("N");
        var sub = new EventStreamSubscription { Id = Guid.NewGuid(), TenantId = tenant, Name = "versions" };
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), TenantId = tenant };
        var key = Guid.NewGuid().ToString("N");
        var definitions = Enumerable.Range(1, 2).Select(v => new ProcessDefinition
        {
            Id = Guid.NewGuid(), Key = key, Version = v, TenantId = tenant, TenantScope = tenant,
            DeploymentId = deployment.Id
        }).ToArray();
        var instances = definitions.Select(d => new ProcessInstance
        {
            Id = Guid.NewGuid(), ProcessDefinitionId = d.Id, TenantId = tenant
        }).ToArray();
        var messages = instances.Select(i => new RuntimeOutboxMessage
        {
            Id = Guid.NewGuid(), ProcessInstanceId = i.Id, TenantId = tenant,
            State = "Published", PublishedAt = DateTime.UtcNow, EventType = "VERSION", Payload = "{}"
        }).ToArray();
        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.AddRange(definitions);
        db.ProcessInstances.AddRange(instances);
        db.EventStreamSubscriptions.Add(sub);
        db.RuntimeOutbox.AddRange(messages);
        await db.SaveChangesAsync(ct);
        await using var host = await Lc09ContractHost.StartAsync(tenant);
        await host.Services.GetRequiredService<RuntimeEventSubscriptionProjectionService>().RunOnceAsync(ct);
        using var http = host.GetTestClient();
        var page = await http.GetFromJsonAsync<EventSubscriptionController.EventStreamPage>(
            $"/api/event-subscriptions/{sub.Id}/events?definitionId={definitions[0].Id}", ct);
        Assert.NotNull(page);
        Assert.Equal(messages[0].Id, Assert.Single(page.Events).SourceEventId);
        using var changedFilter = await http.GetAsync(
            $"/api/event-subscriptions/{sub.Id}/events?after={page.NextCursor}&definitionId={definitions[1].Id}", ct);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, changedFilter.StatusCode);
        await host.StopAsync(ct);
    }
}
