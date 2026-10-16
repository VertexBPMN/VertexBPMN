using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// LC05 runtime slice: optional ApplicationReleaseId binding on execution instances
/// and async operations. Existing instances without an app release must stay valid,
/// and applied migrations must not fabricate a release for legacy rows.
/// </summary>
public sealed class ApplicationInstanceReleaseBindingTests
{
    private const string MigrationBeforeReleaseBinding = "20261015130000_ApplicationDefinitionsAndReleaseCandidates";

    [Fact]
    public async Task Instance_WithoutApplicationRelease_StaysValidAndPersistsNull()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var definition = await AddDefinitionAsync(db, TestContext.Current.CancellationToken);

        var instance = NewInstance(definition.Id, releaseId: null);
        db.ProcessInstances.Add(instance);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var reloaded = await db.ProcessInstances.AsNoTracking()
            .SingleAsync(x => x.Id == instance.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(reloaded); // instance without a release stays valid / loadable
        Assert.Null(reloaded.ApplicationReleaseId);
    }

    [Fact]
    public async Task Instance_WithApplicationRelease_PersistsExactId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var definition = await AddDefinitionAsync(db, TestContext.Current.CancellationToken);
        const string releaseId = "rel-invoicing-release-0001";

        var instance = NewInstance(definition.Id, releaseId);
        db.ProcessInstances.Add(instance);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var reloaded = await db.ProcessInstances.AsNoTracking()
            .SingleAsync(x => x.Id == instance.Id, TestContext.Current.CancellationToken);
        Assert.Equal(releaseId, reloaded.ApplicationReleaseId);
    }

    [Fact]
    public async Task Migration_LegacyInstancesKeepApplicationReleaseIdNull_AfterStoreReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lc05-inst-bind-{Guid.NewGuid():N}.db");
        var definitionId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var token = TestContext.Current.CancellationToken;
        try
        {
            // 1) Build a store at the schema version that predates the release-binding columns
            //    and insert genuine legacy rows (no ApplicationReleaseId column exists yet).
            await using var setup = new SqliteConnection($"Data Source={path};Pooling=False");
            await setup.OpenAsync(token);
            await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(setup).Options);
            await db.GetService<IMigrator>().MigrateAsync(MigrationBeforeReleaseBinding, token);
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = OFF;", token);
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO ProcessInstances (Id, ProcessDefinitionId, BusinessKey, TenantId, StartedAt, EndedAt, State, InstanceId, ProcessId, Status, ActiveTasks, ActiveTokens, Variables, CreatedAt, LastModified, Revision, ParentProcessInstanceId, CallingActivityId) VALUES ({instanceId}, {definitionId}, NULL, 'tenant-a', '2026-01-01T00:00:00', NULL, 'RUNNING', 'legacy-instance', 'invoice', 0, '[]', '[]', '{{}}', '2026-01-01T00:00:00', '2026-01-01T00:00:00', 0, NULL, NULL);", token);
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO Jobs (Id, ProcessInstanceId, ActivityId, Type, DueDate, Retries, ErrorMessage, TenantId, State, Payload, CreatedAt, CompletedAt, LockOwner, LockedUntil, Revision) VALUES ({jobId}, {instanceId}, 'async-task', 'ASYNC', '2026-01-01T00:00:00', 0, NULL, 'tenant-a', 'WAITING', NULL, '2026-01-01T00:00:00', NULL, NULL, NULL, 0);", token);

            // 2) Apply the release-binding migration on the same store.
            await using var apply = new SqliteConnection($"Data Source={path};Pooling=False");
            await apply.OpenAsync(token);
            await using var applyDb = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(apply).Options);
            await applyDb.GetService<IMigrator>().MigrateAsync(cancellationToken: token);

            // 3) Reopen the same SQLite file and verify the migration never fabricated a release.
            await using var reopen = new SqliteConnection($"Data Source={path};Pooling=False");
            await reopen.OpenAsync(token);
            await using var recovered = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(reopen).Options);
            var legacyInstance = await recovered.ProcessInstances.AsNoTracking()
                .SingleAsync(x => x.Id == instanceId, token);
            Assert.NotNull(legacyInstance);
            Assert.Null(legacyInstance.ApplicationReleaseId);

            var legacyJob = await recovered.Jobs.AsNoTracking()
                .SingleAsync(x => x.Id == jobId, token);
            Assert.NotNull(legacyJob);
            Assert.Null(legacyJob.ApplicationReleaseId);
        }
        finally { File.Delete(path); }
    }

    private static ProcessInstance NewInstance(Guid definitionId, string? releaseId) => new()
    {
        Id = Guid.NewGuid(),
        ProcessDefinitionId = definitionId,
        State = "RUNNING",
        InstanceId = Guid.NewGuid().ToString("N"),
        ProcessId = "invoice",
        TenantId = "tenant-a",
        ApplicationReleaseId = releaseId,
    };

    private static async Task<ProcessDefinition> AddDefinitionAsync(BpmnDbContext db, CancellationToken token)
    {
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "binding", TenantId = "tenant-a" };
        db.EngineDeployments.Add(deployment);
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Key = "invoice",
            Name = "Invoice",
            TenantId = "tenant-a",
            TenantScope = "tenant-a",
            DeploymentId = deployment.Id,
            BpmnXml = "<definitions/>",
        };
        db.ProcessDefinitions.Add(definition);
        await db.SaveChangesAsync(token);
        return definition;
    }
}
