using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Tests.Infrastructure;
using VertexBPMN.Tests.Unit.Infrastructure;

namespace VertexBPMN.Tests.Integration.Applications;

internal sealed class EndpointRuntimeFixture : IAsyncDisposable
{
    private readonly EndpointCheckpointStore _store = new("/home/azureuser/.hermes/cache/scratch/lc09b-s2");
    private CustomWebApplicationFactory? _factory;
    internal HttpClient Http { get; private set; } = null!;
    internal string Key { get; } = "runtime-" + Guid.NewGuid().ToString("N");
    internal string Id { get; private set; } = "";
    internal async Task OpenAsync(string content, bool publish = true)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = _store.Open();
        await db.Database.MigrateAsync(ct);
        var deployment = new EngineDeployment { Name = Key, TenantId = "tenant-a" };
        var process = new ProcessDefinition { Key = Key, Name = Key, TenantId = "tenant-a", Deployment = deployment };
        var instance = new ProcessInstance { ProcessDefinition = process, TenantId = "tenant-a" };
        db.ProcessInstances.Add(instance);
        db.Tasks.Add(new UserTask { Name = "Invoice Alpha", ActivityId = "approve", Type = "userTask",
            TenantId = "tenant-a", CandidateRole = "ReadOnly", ProcessInstanceId = instance.Id });
        db.Tasks.Add(new UserTask { Name = "Contract Beta", ActivityId = "approve", Type = "userTask",
            TenantId = "tenant-a", CandidateRole = "ReadOnly", ProcessInstanceId = instance.Id });
        await db.SaveChangesAsync(ct);
        _factory = new CustomWebApplicationFactory().WithPersistentBpmnDatabase(db.Database.GetDbConnection().ConnectionString);
        Http = _factory.CreateClient();
        using var created = await Http.PostAsJsonAsync("/api/applications",
            new { tenantId = "tenant-a", key = Key, name = Key, expectedVersion = 0, content }, ct);
        created.EnsureSuccessStatusCode();
        Id = (await created.Content.ReadFromJsonAsync<ApplicationDefinitionMetadata>(ct))!.Id;
        if (publish)
        {
            using var prepared = await Http.PostAsync($"/api/applications/{Id}/revisions/1/release-candidate?tenantId=tenant-a", null, ct);
            prepared.EnsureSuccessStatusCode();
            var candidate = (await prepared.Content.ReadFromJsonAsync<ApplicationReleaseCandidateRecord>(ct))!;
            using var released = await Http.PostAsync($"/api/applications/release-candidates/{candidate.Id}/release?tenantId=tenant-a", null, ct);
            released.EnsureSuccessStatusCode();
        }
        Http.DefaultRequestHeaders.Add("X-Test-User", "reader");
        Http.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-a");
    }
    internal async Task ActivateRevisionAsync(int revision)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = _store.Open();
        var releases = new VertexBPMN.Infrastructure.Persistence.Services.PersistentApplicationReleaseService(db);
        var manifest = ReleaseManifest.Serialize(new ReleaseManifest
            { Key = Key, Revision = revision, Hash = ReleaseManifest.ComputeHash(EndpointRuntimeContent.Json) });
        var operation = Guid.NewGuid().ToString("N");
        await releases.PrepareAsync(operation, "tenant-a", Id, revision, manifest, ct);
        await releases.ActivateAsync(operation, "tenant-a", Id, ct);
    }
    public async ValueTask DisposeAsync()
    {
        Http?.Dispose();
        if (_factory is not null) { await _factory.DisposeAsync(); }
        await _store.DisposeAsync();
    }
}
