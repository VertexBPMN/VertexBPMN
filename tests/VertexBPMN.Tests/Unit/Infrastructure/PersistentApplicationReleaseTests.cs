using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class PersistentApplicationReleaseTests
{
    private static ApplicationAccessContext Access { get; } = new("editor", new HashSet<string>(["application.design", "application.publish"], StringComparer.Ordinal));
    [Fact]
    public async Task ReleaseManifest_PinsSnapshotHashAndArtifactIdentity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        var candidate = await service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken);
        using var manifest = System.Text.Json.JsonDocument.Parse(candidate.ManifestJson);
        Assert.Equal(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        var artifact = Assert.Single(manifest.RootElement.GetProperty("artifacts").EnumerateArray());
        Assert.Equal("application", artifact.GetProperty("kind").GetString());
        Assert.Equal(candidate.ApplicationRevisionId, artifact.GetProperty("revisionId").GetString());
        Assert.Equal((await service.GetRevisionAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken))!.ContentHash, artifact.GetProperty("hash").GetString());
    }

    [Fact]
    public async Task Release_RevalidatesPinnedReferences()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        db.FormDefinitions.Add(new FormDefinitionRecord { Id = "form", TenantId = "tenant-a", Key = "form", Name = "Form" });
        db.FormDefinitionRevisions.Add(new FormDefinitionRevisionRecord { Id = "revision", FormDefinitionId = "form", TenantId = "tenant-a", Key = "form", Name = "Form", Version = 1 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, """{"references":[{"type":"form","id":"revision","tenantId":"tenant-a"}]}"""), TestContext.Current.CancellationToken);
        var candidate = await service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM FormDefinitionRevisions WHERE Id='revision'", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<VertexBPMN.Domain.Exceptions.ApplicationValidationException>(() => service.ReleaseAsync("tenant-a", candidate.Id, TestContext.Current.CancellationToken));
        Assert.Null(await service.GetReleasedRevisionAsync("tenant-a", draft.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReleaseCandidate_MutationCannotRebindRevision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        var candidate = await service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken);
        candidate.ManifestJson = "{\"tampered\":true}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Manifest_IncludesReferencedArtifactHashes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        db.FormDefinitions.Add(new FormDefinitionRecord { Id = "form", TenantId = "tenant-a", Key = "form", Name = "Form" });
        db.FormDefinitionRevisions.Add(new FormDefinitionRevisionRecord { Id = "revision", FormDefinitionId = "form", TenantId = "tenant-a", Key = "form", Name = "Form", SchemaHash = "form-hash", Version = 1 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, """{"references":[{"type":"form","id":"revision","tenantId":"tenant-a"}]}"""), TestContext.Current.CancellationToken);
        var candidate = await service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken);
        Assert.Contains("form-hash", candidate.ManifestJson, StringComparison.Ordinal);
        Assert.Contains("revision", candidate.ManifestJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReleaseReference_DatabasePreventsDeletingPinnedRevision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        await service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM ApplicationRevisions", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Portal_OnlyReadsReleasedPinnedRevisionWithinTenant()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        Assert.Null(await service.GetReleasedRevisionAsync("tenant-a", draft.Id, TestContext.Current.CancellationToken));
        var candidate = await service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken);
        Assert.Null(await service.GetReleasedRevisionAsync("tenant-a", draft.Id, TestContext.Current.CancellationToken));
        Assert.Null(await service.GetReleaseCandidateAsync("tenant-b", candidate.Id, TestContext.Current.CancellationToken));
        Assert.False(await service.ReleaseAsync("tenant-b", candidate.Id, TestContext.Current.CancellationToken));
        Assert.True(await service.ReleaseAsync("tenant-a", candidate.Id, TestContext.Current.CancellationToken));
        await service.UpdateAsync("tenant-a", draft.Id, new("invoice", "New draft", 1, "{\"changed\":true}"), TestContext.Current.CancellationToken);
        var released = await service.GetReleasedRevisionAsync("tenant-a", draft.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(released);
        Assert.Equal("{}", released.ContentJson);
        Assert.Equal(1, released.Version);
        Assert.Null(await service.GetReleasedRevisionAsync("tenant-b", draft.Id, TestContext.Current.CancellationToken));
    }
}
