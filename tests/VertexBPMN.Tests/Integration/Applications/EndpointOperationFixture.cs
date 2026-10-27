using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Tests.Infrastructure;
using VertexBPMN.Tests.Unit.Infrastructure;

namespace VertexBPMN.Tests.Integration.Applications;

// Real controller/persistence contract with a real (seeded) process definition; the
// principal is xUnit contract evidence (X-Test-User/X-Test-Tenant), not OIDC.
internal sealed class EndpointOperationFixture : IAsyncDisposable
{
    private readonly EndpointCheckpointStore _store = new("/home/azureuser/.hermes/cache/scratch/lc09b-ms2a");
    internal VertexBPMN.Infrastructure.Persistence.BpmnDbContext OpenDb() => _store.Open();
    internal IServiceProvider Services => _factory!.Services;
    private CustomWebApplicationFactory? _factory;
    internal HttpClient Http { get; private set; } = null!;
    internal string Key { get; } = "op-" + Guid.NewGuid().ToString("N");
    internal string ProcessKey { get; } = "op-process-" + Guid.NewGuid().ToString("N");
    internal Guid ProcessId { get; } = Guid.NewGuid();
    internal string Id { get; private set; } = "";
    internal string Content { get; private set; } = "";

    internal async Task OpenAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        Content = EndpointOperationContent.Json(ProcessKey, ProcessId.ToString());
        await using var db = _store.Open();
        await db.Database.MigrateAsync(ct);
        var deployment = new EngineDeployment { Name = Key, TenantId = "tenant-a" };
        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.Add(new ProcessDefinition
        {
            Id = ProcessId, Key = ProcessKey, Name = ProcessKey, Version = 1, TenantId = "tenant-a",
            Deployment = deployment, DeploymentId = deployment.Id,
            BpmnXml = EndpointOperationContent.Bpmn(ProcessKey).Replace("<startEvent id=\"start\" />",
                "<startEvent id=\"start\" /><userTask id=\"wait\" name=\"Review\" /><endEvent id=\"end\" />" +
                "<sequenceFlow id=\"f1\" sourceRef=\"start\" targetRef=\"wait\" />" +
                "<sequenceFlow id=\"f2\" sourceRef=\"wait\" targetRef=\"end\" />", StringComparison.Ordinal)
        });
        await db.SaveChangesAsync(ct);
        _factory = new CustomWebApplicationFactory().WithPersistentBpmnDatabase(db.Database.GetDbConnection().ConnectionString)
            .WithTestServices(services =>
            {
                foreach (var descriptor in services.Where(x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                    && x.ImplementationType == typeof(VertexBPMN.Application.JobExecutorService)).ToArray())
                { services.Remove(descriptor); }
                services.AddSingleton<VertexBPMN.Application.JobExecutorService>();
            });
        Http = _factory.CreateClient();
        using var created = await Http.PostAsJsonAsync("/api/applications",
            new { tenantId = "tenant-a", key = Key, name = Key, expectedVersion = 0, content = Content }, ct);
        created.EnsureSuccessStatusCode();
        Id = (await created.Content.ReadFromJsonAsync<ApplicationDefinitionMetadata>(ct))!.Id;
        await ActivateRevisionAsync(1);
        Http.DefaultRequestHeaders.Add("X-Test-User", "reader");
        Http.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-a");
    }

    internal async Task ActivateRevisionAsync(int revision)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = _store.Open();
        var releases = new VertexBPMN.Infrastructure.Persistence.Services.PersistentApplicationReleaseService(db);
        var manifest = ReleaseManifest.Serialize(new ReleaseManifest
            { Key = Key, Revision = revision, Hash = ReleaseManifest.ComputeHash(Content) });
        var operation = Guid.NewGuid().ToString("N");
        await releases.PrepareAsync(operation, "tenant-a", Id, revision, manifest, ct);
        await releases.ActivateAsync(operation, "tenant-a", Id, ct);
    }

    internal async Task<ApplicationEndpointOperation?> FindOperationAsync(string operationId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = _store.Open();
        return await db.ApplicationEndpointOperations.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OperationId == operationId, ct);
    }

    public async ValueTask DisposeAsync()
    {
        Http?.Dispose();
        if (_factory is not null) { await _factory.DisposeAsync(); }
        await _store.DisposeAsync();
    }
}
