using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class PersistentApplicationValidationTests
{
    private static ApplicationAccessContext Access { get; } = new("editor", new HashSet<string>(["application.design", "application.publish"], StringComparer.Ordinal));
    [Fact]
    public async Task Save_RejectsUnknownComponentVersionWithoutWriting()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        const string content = """{"pages":[{"id":"home","route":"/home","components":[{"type":"table","version":"99"}]}]}""";
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, content), TestContext.Current.CancellationToken));
        Assert.Empty(await db.ApplicationDefinitions.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_RejectsForeignTenantReference()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        await Assert.ThrowsAsync<ApplicationForeignTenantException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, """{"references":[{"type":"form","id":"foreign","tenantId":"tenant-b"}]}"""), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_RejectsReferenceCycle()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, """{"pages":[{"id":"a","route":"/a","navigation":["b"]},{"id":"b","route":"/b","navigation":["a"]}]}"""), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DomainInvalidDraft_PersistsDiagnosticsButCannotRelease()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, """{"pages":[{"id":"home","route":"/home","components":[{"type":"table","version":"1.0.0","dataSource":"missing"}]}]}"""), TestContext.Current.CancellationToken);
        var revision = await service.GetRevisionAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken);
        Assert.NotNull(revision);
        Assert.Contains("UNKNOWN_DATA_SOURCE", revision.DiagnosticsJson, StringComparison.Ordinal);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_RevalidatesMetadataBeforeChangingDraft()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.UpdateAsync("tenant-a", draft.Id, new("invoice", "Invalid", 1, """{"pages":[{"id":"home","route":"/../admin"}]}"""), TestContext.Current.CancellationToken));
        Assert.Equal(1, (await service.GetAsync("tenant-a", draft.Id, TestContext.Current.CancellationToken))!.Version);
        Assert.Single(await db.ApplicationRevisions.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_RejectsForgedTenantOnExistingFormReference()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        db.FormDefinitions.Add(new FormDefinitionRecord { Id = "foreign-form", TenantId = "tenant-b", Key = "foreign", Name = "Foreign" });
        db.FormDefinitionRevisions.Add(new FormDefinitionRevisionRecord { Id = "foreign-revision", FormDefinitionId = "foreign-form", TenantId = "tenant-b", Key = "foreign", Name = "Foreign", Version = 1 });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var revision = await db.FormDefinitionRevisions.SingleAsync(TestContext.Current.CancellationToken);
        var content = System.Text.Json.JsonSerializer.Serialize(new { references = new[] { new { type = "form", id = revision.Id, tenantId = "tenant-a" } } });
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        await Assert.ThrowsAsync<ApplicationForeignTenantException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, content), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Localization_RejectsNonStringTranslation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, """{"localizations":{"de":{"title":{"personalExample":"private"}}}}"""), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_RejectsMalformedMetadataWithExplicitError()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{invalid}"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_RejectsEmptyTenantAndOversizedContent()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateAsync("", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, new string(' ', 500_001)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Catalog_RejectsUnknownActionVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, """{"pages":[{"id":"home","route":"/home","actions":[{"type":"navigate","version":"99"}]}]}"""), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_AcceptsPinnedProcessReference()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), TenantId = "tenant-a", Name = "Test" };
        var process = new ProcessDefinition { TenantId = "tenant-a", TenantScope = "tenant-a", Key = "invoice", Name = "Invoice", DeploymentId = deployment.Id, Version = 1 };
        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.Add(process);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var content = System.Text.Json.JsonSerializer.Serialize(new { references = new[] { new { type = "process", id = process.Id.ToString(), tenantId = "tenant-a" } } });
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, content), TestContext.Current.CancellationToken);
        Assert.NotNull(await service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DomainInvalidProcessAction_CannotRelease()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, """{"pages":[{"id":"home","route":"/home","actions":[{"type":"start-process","version":"1.0.0","processDefinitionId":"missing"}]}]}"""), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_RejectsDuplicateRoutes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, """{"pages":[{"id":"a","route":"/same"},{"id":"b","route":"/same"}]}"""), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_RejectsInvalidRoute()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, """{"pages":[{"id":"home","route":"/../admin"}]}"""), TestContext.Current.CancellationToken));
    }
}
