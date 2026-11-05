using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Tests.Infrastructure;
using VertexBPMN.Tests.Unit.Infrastructure;

namespace VertexBPMN.Tests.Integration.Applications;

// Real controller/persistence contract, test principal only; no OIDC/browser claim.
public sealed class ApplicationEndpointApiTests
{
    [Fact]
    public async Task ExistingDraftApi_SavesEndpointRevisionAndRejectsForeignBinding()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new EndpointCheckpointStore();
        await using var db = store.Open();
        await db.Database.MigrateAsync(ct);
        await using var factory = new CustomWebApplicationFactory()
            .WithPersistentBpmnDatabase(db.Database.GetDbConnection().ConnectionString);
        using var http = factory.CreateClient();
        var key = "api-endpoint-" + Guid.NewGuid().ToString("N");
        using var create = await http.PostAsJsonAsync("/api/applications",
            new { tenantId = "tenant-a", key, name = key, expectedVersion = 0, content = "{}" }, ct);
        create.EnsureSuccessStatusCode();
        var draft = (await create.Content.ReadFromJsonAsync<ApplicationDefinitionMetadata>(ct))!;
        using var saved = await http.PutAsJsonAsync($"/api/applications/{draft.Id}",
            new { tenantId = "tenant-a", key, name = key, expectedVersion = 1, content = EndpointCheckpointStore.Content() }, ct);
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        var revision = (await http.GetFromJsonAsync<ApplicationRevisionRecord>(
            $"/api/applications/{draft.Id}/revisions/2?tenantId=tenant-a", ct))!;
        Assert.Equal("customers", Assert.Single(ApplicationEndpointMetadata.Read(revision.ContentJson)).Key);
        using var denied = await http.PutAsJsonAsync($"/api/applications/{draft.Id}",
            new { tenantId = "tenant-a", key, name = key, expectedVersion = 2,
                content = EndpointCheckpointStore.Content(target: "foreign-customers") }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using var loaded = await http.GetAsync($"/api/applications/{draft.Id}?tenantId=tenant-a", ct);
        var current = (await loaded.Content.ReadFromJsonAsync<ApplicationDefinitionMetadata>(ct))!;
        Assert.Equal(2, current.Version);
        Assert.Equal(revision.ContentJson, current.Content);
    }
}
