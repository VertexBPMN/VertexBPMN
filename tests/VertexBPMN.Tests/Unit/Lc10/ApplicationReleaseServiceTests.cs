using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;
using Xunit;

#pragma warning disable RCS1266 // JSON test fixtures require escaped quotes; raw strings conflict with interpolation braces.

namespace VertexBPMN.Tests.Unit.Lc10;

/// <summary>
/// LC10 release activation state machine tests against a real SQLite store (shared-memory
/// connection stays open for the class lifetime). Uses EnsureCreated so the ApplicationReleases
/// table is created from the current model — mirroring the repo's atomic-CAS + reconciliation
/// idiom rather than distributed SQL transactions.
/// </summary>
public sealed class ApplicationReleaseServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new($"Data Source=lc10-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False");
    private const string Tenant = "t1";
    private const string AppId = "app1";
    private const string AppKey = "k1";
    private const int Revision = 1;
    private readonly Guid _processId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Content(Guid processId, string? extraRefs = null)
    {
#pragma warning disable RCS1266 // JSON requires escaped quotes; raw strings conflict with interpolation braces.
        var references = "[{\"type\":\"process\",\"key\":\"SampleProcess\",\"id\":\"" + processId + "\",\"revision\":1}";
        if (extraRefs is not null) { references += "," + extraRefs; }
        references += "]";
        return "{\"references\":" + references + "}";
#pragma warning restore RCS1266
    }

    private static string MissingFormReference => "{ \"type\":\"form\",\"key\":\"MissingForm\",\"revision\":2 }";

    private static string Manifest(string key, int revision) =>
        "{\"schemaVersion\":\"1\",\"artifactKind\":\"application\",\"key\":\"" + key + "\",\"revision\":" + revision + ",\"hash\":\"deadbeef\"}";

    public ApplicationReleaseServiceTests()
    {
        _connection.Open();
        using var db = NewContext();
        db.Database.EnsureCreated();
        Seed(db);
    }

    private BpmnDbContext NewContext() => CreateContext(_connection);

    private static BpmnDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "The release service owns the context for the test's lifetime.")]
    private PersistentApplicationReleaseService NewService() => new(NewContext());

    private void Seed(BpmnDbContext db)
    {
        db.ApplicationDefinitions.Add(new ApplicationDefinitionRecord
        {
            Id = AppId,
            TenantId = Tenant,
            Key = AppKey,
            Name = "Sample App",
            ContentJson = "{}",
            Version = 1,
        });

        db.ApplicationRevisions.Add(new ApplicationRevisionRecord
        {
            Id = "rev1",
            ApplicationDefinitionId = AppId,
            TenantId = Tenant,
            ContentJson = Content(_processId),
            ContentHash = "h",
            Version = Revision,
        });

        var deployment = new EngineDeployment
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Dep",
            CreatedAt = DateTime.UtcNow,
        };
        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.Add(new ProcessDefinition
        {
            Id = _processId,
            Key = "SampleProcess",
            Name = "Sample Process",
            Version = 1,
            BpmnXml = "<definitions/>",
            TenantId = Tenant,
            CreatedAt = DateTime.UtcNow,
            DeploymentId = deployment.Id,
            Deployment = deployment,
        });

        db.SaveChanges();
    }

    [Fact]
    public async Task Prepare_CreatesPreparedRelease_AndIsIdempotentOnSameOperationId()
    {
        var service = NewService();
        var first = await service.PrepareAsync("op-1", Tenant, AppId, Revision, Manifest(AppKey, Revision), Ct);
        var second = await service.PrepareAsync("op-1", Tenant, AppId, Revision, Manifest(AppKey, Revision), Ct);

        Assert.Equal(ApplicationReleaseState.Prepared, first.State);
        Assert.Equal(first.OperationId, second.OperationId);
    }

    [Fact]
    public async Task Prepare_WithMissingReference_ThrowsAndKeepsVisiblePreparedRelease()
    {
        // A second application whose revision references a non-existent form — the reference
        // graph is non-satisfiable, so preparation must be blocked (revisions are immutable).
        await using (var seeder = NewContext())
        {
            seeder.ApplicationDefinitions.Add(new ApplicationDefinitionRecord
            {
                Id = "app2",
                TenantId = Tenant,
                Key = "k2",
                Name = "Blocked App",
                ContentJson = "{}",
                Version = 1,
            });
            seeder.ApplicationRevisions.Add(new ApplicationRevisionRecord
            {
                Id = "rev-bad",
                ApplicationDefinitionId = "app2",
                TenantId = Tenant,
                ContentJson = Content(_processId, MissingFormReference),
                ContentHash = "h-bad",
                Version = 1,
            });
            await seeder.SaveChangesAsync(Ct);
        }

        var service = NewService();
        var exception = await Assert.ThrowsAsync<ApplicationReleaseCompatibilityException>(
            () => service.PrepareAsync("op-2", Tenant, "app2", 1, Manifest("k2", 1), Ct));

        Assert.NotNull(exception.Release);
        Assert.Equal(ApplicationReleaseState.Prepared, exception.Release.State);
        Assert.False(string.IsNullOrEmpty(exception.Release.DiagnosticsJson));
    }

    [Fact]
    public async Task Activate_TransitionsToActive_AndBindsTenantCheckedProcessDefinitionId()
    {
        var service = NewService();
        await service.PrepareAsync("op-3", Tenant, AppId, Revision, Manifest(AppKey, Revision), Ct);
        var active = await service.ActivateAsync("op-3", Tenant, AppId, Ct);

        Assert.Equal(ApplicationReleaseState.Active, active.State);
        Assert.Equal(_processId.ToString(), active.BoundProcessDefinitionId);
    }

    [Fact]
    public async Task NoProductiveWork_UntilActive_ResolveReturnsNullThenBoundId()
    {
        var service = NewService();
        Assert.Null(await service.ResolveActiveProcessDefinitionIdAsync(Tenant, AppId, Ct));

        await service.PrepareAsync("op-4", Tenant, AppId, Revision, Manifest(AppKey, Revision), Ct);
        // Prepared / DeploymentRunning / Ready candidate is NOT active yet: no productive-work bound entry.
        Assert.Null(await service.ResolveActiveProcessDefinitionIdAsync(Tenant, AppId, Ct));

        await service.ActivateAsync("op-4", Tenant, AppId, Ct);
        Assert.Equal(_processId.ToString(), await service.ResolveActiveProcessDefinitionIdAsync(Tenant, AppId, Ct));
    }

    [Fact]
    public async Task RepeatedActivateWithSameOperationId_YieldsOneActiveGeneration()
    {
        var service = NewService();
        await service.PrepareAsync("op-5", Tenant, AppId, Revision, Manifest(AppKey, Revision), Ct);
        var first = await service.ActivateAsync("op-5", Tenant, AppId, Ct);
        var second = await service.ActivateAsync("op-5", Tenant, AppId, Ct);

        Assert.Equal(ApplicationReleaseState.Active, first.State);
        Assert.Equal(ApplicationReleaseState.Active, second.State);
        Assert.Equal(first.PointerRevision, second.PointerRevision);

        await using var db = NewContext();
        var actives = await db.ApplicationReleases.CountAsync(x => x.State == ApplicationReleaseState.Active, Ct);
        Assert.Equal(1, actives);
    }

    [Fact]
    public async Task TwoReplicas_RepeatedActivation_ConvergeToOneConsistentActive()
    {
        var serviceA = NewService();
        var serviceB = NewService();
        await serviceA.PrepareAsync("op-6", Tenant, AppId, Revision, Manifest(AppKey, Revision), Ct);

        var taskA = serviceA.ActivateAsync("op-6", Tenant, AppId, Ct);
        var taskB = serviceB.ActivateAsync("op-6", Tenant, AppId, Ct);
        await Task.WhenAll(taskA, taskB);

        await using var db = NewContext();
        var actives = await db.ApplicationReleases.Where(x => x.State == ApplicationReleaseState.Active).ToListAsync(Ct);
        Assert.Single(actives);

        var status = await serviceA.GetStatusAsync(Tenant, AppId, Ct);
        Assert.NotNull(status);
        Assert.Equal(ApplicationReleaseState.Active, status.State);
    }

    [Fact]
    public async Task SecondGenerationActivation_AndRollback_SwitchesNewStartsBackToPrior()
    {
        var service = NewService();
        await service.PrepareAsync("op-7a", Tenant, AppId, 1, Manifest(AppKey, 1), Ct);
        var release1 = await service.ActivateAsync("op-7a", Tenant, AppId, Ct);
        Assert.Equal(ApplicationReleaseState.Active, release1.State);

        // Second generation: revision 2.
        await using (var updater = NewContext())
        {
            updater.ApplicationRevisions.Add(new ApplicationRevisionRecord
            {
                Id = "rev2",
                ApplicationDefinitionId = AppId,
                TenantId = Tenant,
                ContentJson = Content(_processId),
                ContentHash = "h2",
                Version = 2,
            });
            await updater.SaveChangesAsync(Ct);
        }

        await service.PrepareAsync("op-7b", Tenant, AppId, 2, Manifest(AppKey, 2), Ct);
        var release2 = await service.ActivateAsync("op-7b", Tenant, AppId, Ct);
        Assert.Equal(ApplicationReleaseState.Active, release2.State);

        // New starts currently bind the active release (release2's generation).
        Assert.Equal(_processId.ToString(), await service.ResolveActiveProcessDefinitionIdAsync(Tenant, AppId, Ct));

        // Rollback switches new starts back to a prior generation.
        var rolledBack = await service.RollbackAsync("op-7r", Tenant, AppId, Ct);
        Assert.NotNull(rolledBack);
        Assert.Equal(ApplicationReleaseState.Active, rolledBack.State);

        // release-2 marked recovered (no longer active); exactly one active generation.
        await using var db = NewContext();
        var generation2 = await db.ApplicationReleases.SingleAsync(x => x.OperationId == "op-7b", Ct);
        Assert.Equal(ApplicationReleaseState.Recovered, generation2.State);
        var actives = await db.ApplicationReleases.CountAsync(x => x.State == ApplicationReleaseState.Active, Ct);
        Assert.Equal(1, actives);
    }

    [Fact]
    public async Task GetStatus_ReconcilesDeploymentRunning_ToReadyOrActive()
    {
        // Simulate a durable operation left in DeploymentRunning by a crashed replica.
        await using (var seeder = NewContext())
        {
            seeder.ApplicationReleases.Add(new ApplicationReleaseRecord
            {
                OperationId = "op-8",
                TenantId = Tenant,
                ApplicationDefinitionId = AppId,
                ApplicationRevisionId = "rev1",
                ManifestJson = Manifest(AppKey, 1),
                State = ApplicationReleaseState.DeploymentRunning,
                DiagnosticsJson = "[]",
                PointerRevision = 1,
            });
            await seeder.SaveChangesAsync(Ct);
        }

        var service = NewService();
        var status = await service.GetStatusAsync(Tenant, AppId, Ct);

        Assert.NotNull(status);
        Assert.True(status.State is ApplicationReleaseState.Ready or ApplicationReleaseState.Active,
            $"Expected reconciliation to advance DeploymentRunning, got {status.State}.");
    }

    public void Dispose() => _connection.Dispose();
}
