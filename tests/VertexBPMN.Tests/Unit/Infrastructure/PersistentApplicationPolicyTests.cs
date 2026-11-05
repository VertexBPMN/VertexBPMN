using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class PersistentApplicationPolicyTests
{
    private static ApplicationAccessContext Access { get; } = new("editor", new HashSet<string>(["application.design", "application.publish"], StringComparer.Ordinal));
    [Fact]
    public async Task Quota_RejectsAdditionalDraft()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), maxDraftsPerTenant: 1, access: Access);
        await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ApplicationDefinitionConflictException>(() => service.CreateAsync("tenant-a", new("second", "Second", 0, "{}"), TestContext.Current.CancellationToken));
        Assert.Single(await db.ApplicationDefinitions.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadOnlyEditor_CannotSaveOrRelease()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var reader = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: new("reader", new HashSet<string>(StringComparer.Ordinal)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.ReleaseAsync("tenant-a", "candidate", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Audit_RecordsEverySuccessfulMutation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var audit = new Mock<IAuditLogService>();
        var service = new PersistentApplicationDefinitionService(db, audit.Object, access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        await service.UpdateAsync("tenant-a", draft.Id, new("invoice", "Updated", 1, "{}"), TestContext.Current.CancellationToken);
        var candidate = await service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 2, TestContext.Current.CancellationToken);
        await service.ReleaseAsync("tenant-a", candidate.Id, TestContext.Current.CancellationToken);
        audit.Verify(x => x.RecordAsync(It.Is<AuditLog>(a => a.TenantId == "tenant-a" && a.Action == "application.updated"), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(x => x.RecordAsync(It.Is<AuditLog>(a => a.TenantId == "tenant-a" && a.Action == "application.prepared"), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(x => x.RecordAsync(It.Is<AuditLog>(a => a.TenantId == "tenant-a" && a.Action == "application.released"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MissingServerIdentity_CannotWrite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PortalIdentity_CannotReadDesignerDraft()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var editor = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await editor.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        var portal = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: new("portal", new HashSet<string>(["application.use"], StringComparer.Ordinal)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => portal.GetAsync("tenant-a", draft.Id, TestContext.Current.CancellationToken));
        Assert.Null(await portal.GetReleasedRevisionAsync("tenant-a", draft.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PortalIdentity_CannotReadUnreleasedRevisionOrCandidate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var editor = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await editor.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        var candidate = await editor.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken);
        var portal = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: new("portal", new HashSet<string>(["application.use"], StringComparer.Ordinal)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => portal.GetRevisionAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => portal.GetReleaseCandidateAsync("tenant-a", candidate.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_ProtectsReferencedApplicationRevision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var target = await service.CreateAsync("tenant-a", new("target", "Target", 0, "{}"), TestContext.Current.CancellationToken);
        var revision = await service.GetRevisionAsync("tenant-a", target.Id, 1, TestContext.Current.CancellationToken);
        var content = System.Text.Json.JsonSerializer.Serialize(new { references = new[] { new { type = "application", id = revision!.Id, tenantId = "tenant-a" } } });
        await service.CreateAsync("tenant-a", new("source", "Source", 0, content), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ApplicationDefinitionConflictException>(() => service.DeleteAsync("tenant-a", target.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_ProtectsReleaseCandidate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        await service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ApplicationDefinitionConflictException>(() => service.DeleteAsync("tenant-a", draft.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(await service.GetAsync("tenant-a", draft.Id, TestContext.Current.CancellationToken));
    }
}
