using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Moq;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class PersistentFormRevisionTests
{
    [Fact]
    public async Task DirectRevisionMutation_IsRejected()
    {
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new PersistentFormDefinitionService(db, Mock.Of<IAuditLogService>());
        await service.CreateAsync("tenant-a", new("approval", "Approval", "{}"), TestContext.Current.CancellationToken);
        var revision = await db.FormDefinitionRevisions.SingleAsync(TestContext.Current.CancellationToken);
        revision.Schema = "{\"tampered\":true}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentUpdate_RejectsStaleVersion()
    {
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var first = new BpmnDbContext(options);
        var service = new PersistentFormDefinitionService(first, Mock.Of<IAuditLogService>());
        var created = await service.CreateAsync("tenant-a", new("approval", "Approval", "{}"), TestContext.Current.CancellationToken);
        await using var second = new BpmnDbContext(options);
        await second.FormDefinitions.SingleAsync(TestContext.Current.CancellationToken);
        await service.UpdateAsync("tenant-a", created.Id, new("approval", "Approval", "{\"first\":true}"), TestContext.Current.CancellationToken);
        var stale = new PersistentFormDefinitionService(second, Mock.Of<IAuditLogService>());
        await Assert.ThrowsAsync<FormDefinitionConflictException>(() => stale.UpdateAsync("tenant-a", created.Id, new("approval", "Approval", "{\"second\":true}"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_ProtectsLegacyTaskReferenceAfterKeyRename()
    {
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new BpmnDbContext(options);
        var service = new PersistentFormDefinitionService(db, Mock.Of<IAuditLogService>());
        var created = await service.CreateAsync("tenant-a", new("approval", "Approval", "{}"), TestContext.Current.CancellationToken);
        db.Tasks.Add(new VertexBPMN.Domain.Entities.UserTask { Id = Guid.NewGuid(), TenantId = "tenant-a", FormKey = "approval" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await service.UpdateAsync("tenant-a", created.Id, new("renamed", "Approval", "{}"), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<FormDefinitionConflictException>(() => service.DeleteAsync("tenant-a", created.Id, TestContext.Current.CancellationToken));
        Assert.NotNull(await service.GetAsync("tenant-a", created.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevisionReads_ReturnHistoricalSchemaWithinTenant()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentFormDefinitionService(db, Mock.Of<IAuditLogService>());
        var created = await service.CreateAsync("tenant-a", new("approval", "Approval", "{}"), TestContext.Current.CancellationToken);
        await service.UpdateAsync("tenant-a", created.Id, new("approval", "Approval", "{\"new\":true}"), TestContext.Current.CancellationToken);
        var value = await service.GetRevisionAsync("tenant-a", "approval", 1, TestContext.Current.CancellationToken);
        Assert.NotNull(value);
        Assert.Equal("{}", value.Schema);
        Assert.Null(await service.GetRevisionAsync("tenant-b", "approval", 1, TestContext.Current.CancellationToken));
        Assert.Null(await service.GetRevisionAsync("tenant-a", "approval", 3, TestContext.Current.CancellationToken));
        Assert.Equal(2, (await service.ListRevisionsAsync("tenant-a", created.Id, TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task Migration_BackfillsOnlyCurrentVersionAndSurvivesReopen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lc04-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite($"Data Source={path}").Options;
        try
        {
            await using (var db = new BpmnDbContext(options))
            {
                var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
                await migrator.MigrateAsync("20261011140107_SourceControlExecutionContracts", TestContext.Current.CancellationToken);
                await db.Database.ExecuteSqlRawAsync("INSERT INTO FormDefinitions (Id,TenantId,Key,Name,Schema,Version,CreatedAt,LastModified) VALUES ('legacy','tenant-a','approval','Approval','{{}}',7,'2026-01-01','2026-01-02')", TestContext.Current.CancellationToken);
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            }
            await using var reopened = new BpmnDbContext(options);
            var revision = Assert.Single(await reopened.FormDefinitionRevisions.ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal(7, revision.Version);
            Assert.Equal("{}", revision.Schema);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("{}"))).ToLowerInvariant(), revision.SchemaHash);
            Assert.False(reopened.Database.HasPendingModelChanges());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task UpdateByKey_AppendsRevisionWithoutChangingPreviousSchema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentFormDefinitionService(db, Mock.Of<IAuditLogService>());
        var created = await service.CreateAsync("tenant-a", new("approval", "Approval", "{}"), TestContext.Current.CancellationToken);
        Assert.True(await service.UpdateAsync("tenant-a", "approval", new("approval", "New", "{\"components\":[]}"), TestContext.Current.CancellationToken));
        var revisions = await db.FormDefinitionRevisions.OrderBy(x => x.Version).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, revisions.Count);
        Assert.Equal(created.Schema, revisions[0].Schema);
        Assert.Equal(2, revisions[1].Version);
        Assert.Equal("{\"components\":[]}", revisions[1].Schema);
    }

    [Fact]
    public async Task Create_PersistsAnImmutableHashedRevision()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentFormDefinitionService(db, Mock.Of<IAuditLogService>());
        var created = await service.CreateAsync("tenant-a", new("approval", "Approval", "{\"components\":[]}"), TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='FormDefinitionRevisions'";
        Assert.Equal(1L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        command.CommandText = "SELECT Schema, SchemaHash, Version, FormDefinitionId FROM FormDefinitionRevisions";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(created.Schema, reader.GetString(0));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(created.Schema))).ToLowerInvariant(), reader.GetString(1));
        Assert.Equal(1, reader.GetInt32(2));
        Assert.Equal(created.Id, reader.GetString(3));
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
    }
}
