using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class PersistentApplicationDefinitionTests
{
    private static ApplicationAccessContext Access { get; } = new("editor", new HashSet<string>(["application.design", "application.publish"], StringComparer.Ordinal));
    [Fact]
    public async Task Create_PersistsHashedRevision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = Database(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var created = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{\"schemaVersion\":1,\"pages\":[]}"), TestContext.Current.CancellationToken);
        var revision = Assert.Single(await db.ApplicationRevisions.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(created.Id, revision.ApplicationDefinitionId);
        Assert.Equal(1, revision.Version);
        Assert.Equal(created.Content, revision.ContentJson);
        Assert.Equal(Hash(created.Content), revision.ContentHash);
    }

    [Fact]
    public async Task TwoEditors_StaleSavePreservesWinnerAndOriginalRevision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var first = Database(connection);
        await first.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(first, Mock.Of<IAuditLogService>(), access: Access);
        var created = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        await using var second = Database(connection);
        await second.ApplicationDefinitions.SingleAsync(TestContext.Current.CancellationToken);
        await service.UpdateAsync("tenant-a", created.Id, new("invoice", "Winner", 1, "{\"winner\":true}"), TestContext.Current.CancellationToken);
        var stale = new PersistentApplicationDefinitionService(second, Mock.Of<IAuditLogService>(), access: Access);
        await Assert.ThrowsAsync<VertexBPMN.Domain.Exceptions.ApplicationDefinitionConflictException>(() => stale.UpdateAsync("tenant-a", created.Id, new("invoice", "Loser", 1, "{\"loser\":true}"), TestContext.Current.CancellationToken));
        await using var verify = Database(connection);
        var draft = await verify.ApplicationDefinitions.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Winner", draft.Name);
        Assert.Equal(2, draft.Version);
        var revisions = await verify.ApplicationRevisions.OrderBy(x => x.Version).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, revisions.Count);
        Assert.Equal("{}", revisions[0].ContentJson);
        Assert.Equal("{\"winner\":true}", revisions[1].ContentJson);
    }

    [Fact]
    public async Task RevisionMutation_IsRejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = Database(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        var revision = await db.ApplicationRevisions.SingleAsync(TestContext.Current.CancellationToken);
        revision.ContentJson = "{\"tampered\":true}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ForeignTenant_CannotReadDraftOrRevision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = Database(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        Assert.Null(await service.GetAsync("tenant-b", draft.Id, TestContext.Current.CancellationToken));
        Assert.Null(await service.GetRevisionAsync("tenant-b", draft.Id, 1, TestContext.Current.CancellationToken));
        Assert.NotNull(await service.GetAsync("tenant-a", draft.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(await service.GetRevisionAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Create_RejectsNonzeroExpectedVersion()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = Database(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        await Assert.ThrowsAsync<VertexBPMN.Domain.Exceptions.ApplicationDefinitionConflictException>(() => service.CreateAsync("tenant-a", new("invoice", "Invoice", 7, "{}"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_ChangesKeyAndDuplicateKeyConflicts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = Database(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        await service.UpdateAsync("tenant-a", draft.Id, new("renamed", "Renamed", 1, "{}"), TestContext.Current.CancellationToken);
        Assert.Equal("renamed", (await service.GetAsync("tenant-a", draft.Id, TestContext.Current.CancellationToken))!.Key);
        await Assert.ThrowsAsync<VertexBPMN.Domain.Exceptions.ApplicationDefinitionConflictException>(() => service.CreateAsync("tenant-a", new("renamed", "Duplicate", 0, "{}"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DraftRead_ReturnsPersistedDomainDiagnostics()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = Database(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var created = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, """{"pages":[{"id":"home","route":"/home","components":[{"type":"table","version":"1.0.0","dataSource":"missing"}]}]}"""), TestContext.Current.CancellationToken);
        var read = await service.GetAsync("tenant-a", created.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(read);
        Assert.Contains("UNKNOWN_DATA_SOURCE", read.DiagnosticsJson, StringComparison.Ordinal);
    }

    private static BpmnDbContext Database(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);

    private static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
