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

public sealed class PersistentApplicationRecoveryTests
{
    private static ApplicationAccessContext Access { get; } = new("editor", new HashSet<string>(["application.design", "application.publish"], StringComparer.Ordinal));
    [Fact]
    public async Task Migration_AddsApplicationTablesWithoutChangingLegacyRows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlRawAsync("DROP TABLE ApplicationReleaseCandidates; DROP TABLE ApplicationRevisions; DROP TABLE ApplicationDefinitions;", TestContext.Current.CancellationToken);
        var migration = new VertexBPMN.Infrastructure.Persistence.Migrations.Bpmn.ApplicationDefinitionsAndReleaseCandidates();
        var generator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsSqlGenerator>(db);
        foreach (var command in generator.Generate(migration.UpOperations, db.Model))
        {
            await db.Database.ExecuteSqlRawAsync(command.CommandText, TestContext.Current.CancellationToken);
        }
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, "{}"), TestContext.Current.CancellationToken);
        Assert.NotNull(await service.CreateReleaseCandidateAsync("tenant-a", draft.Id, 1, TestContext.Current.CancellationToken));
        Assert.Equal(3, await db.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ModelSnapshot_MatchesCurrentApplicationSchema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Restart_ReopenedConnectionRetainsExactRevisionHash()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lc05-{Guid.NewGuid():N}.db");
        string id;
        string before;
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
                await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
                var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
                const string content = """{ "pages": [], "localizations": {"de":{"title":"Rechnungsprüfung"}}, "schemaVersion":1 }""";
                var draft = await service.CreateAsync("tenant-a", new("invoice", "Invoice", 0, content), TestContext.Current.CancellationToken);
                id = draft.Id;
                var revision = await service.GetRevisionAsync("tenant-a", id, 1, TestContext.Current.CancellationToken);
                Assert.NotNull(revision);
                before = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(revision.ContentJson))).ToLowerInvariant();
                Assert.Equal(before, revision.ContentHash);
            }
            await using var reopened = new SqliteConnection($"Data Source={path};Pooling=False");
            await reopened.OpenAsync(TestContext.Current.CancellationToken);
            await using var recovered = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(reopened).Options);
            var loaded = await new PersistentApplicationDefinitionService(recovered, Mock.Of<IAuditLogService>(), access: Access).GetRevisionAsync("tenant-a", id, 1, TestContext.Current.CancellationToken);
            Assert.NotNull(loaded);
            Assert.Equal(before, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(loaded.ContentJson))).ToLowerInvariant());
            Assert.Equal(before, loaded.ContentHash);
        }
        finally { File.Delete(path); }
    }
}
