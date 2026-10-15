using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class LegacyTaskFormHashMigrationTests
{
    private const string LegacyMigration = "20261011140107_SourceControlExecutionContracts";
    private const string CurrentSchema = "{\"current\":true}";
    private const string EmptyObject = "{}";
    private const string FrozenSchema = "{\"label\":\"Grüße 東京 😀\", \"components\": []}\n";

    [Fact]
    public async Task Migration_HashesFrozenLegacyTaskSchemasWithoutInventingRevisionMetadata()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lc04-legacy-task-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite($"Data Source={path};Pooling=False");
        try
        {
            await using (var db = new BpmnDbContext(options.Options))
            {
                await db.GetService<IMigrator>().MigrateAsync(LegacyMigration, TestContext.Current.CancellationToken);
                await SeedLegacyTasksAsync(db);
                await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
            }
            await using var reopened = new BpmnDbContext(options.Options);
            var tasks = await reopened.Tasks.Where(x => x.Name == "legacy-hash-test")
                .OrderBy(x => x.FormKey).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(3, tasks.Count);
            var frozen = Assert.Single(tasks, x => x.FormKey == "approval");
            Assert.Equal(FrozenSchema, frozen.FormSchema);
            Assert.Equal(Hash(FrozenSchema), frozen.FormSchemaHash);
            var empty = Assert.Single(tasks, x => x.FormKey == "empty");
            Assert.Equal(Hash(string.Empty), empty.FormSchemaHash);
            var absent = Assert.Single(tasks, x => x.FormKey == "no-schema");
            Assert.Null(absent.FormSchemaHash);
            Assert.All(tasks, task =>
            {
                Assert.Null(task.FormVersion);
                Assert.Null(task.FormRevisionId);
            });
            var revision = Assert.Single(await reopened.FormDefinitionRevisions
                .Where(x => x.FormDefinitionId == "legacy-hash-form").ToListAsync(TestContext.Current.CancellationToken));
            Assert.Equal(7, revision.Version);
            Assert.Equal(Hash("{\"current\":true}"), revision.SchemaHash);
            Assert.NotEqual(revision.SchemaHash, frozen.FormSchemaHash, StringComparer.Ordinal);
            Assert.False(reopened.Database.HasPendingModelChanges());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SqlServerMigration_CollatesUnicodeSchemaBeforeVarcharConversion()
    {
        var options = new DbContextOptionsBuilder<BpmnDbContext>()
            .UseSqlServer("Server=localhost;Database=lc04_sql_generation;Trusted_Connection=True;TrustServerCertificate=True");
        using var db = new BpmnDbContext(options.Options);
        var sql = db.GetService<IMigrator>().GenerateScript(LegacyMigration, "20261015115634_TaskFormRevisionSnapshots");
        Assert.Contains("CONVERT(varchar(max), [Schema] COLLATE Latin1_General_100_BIN2_UTF8)", sql, StringComparison.Ordinal);
        Assert.Contains("CONVERT(varchar(max), [FormSchema] COLLATE Latin1_General_100_BIN2_UTF8)", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CONVERT(varchar(max), [Schema]) COLLATE", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CONVERT(varchar(max), [FormSchema]) COLLATE", sql, StringComparison.Ordinal);
    }

    private static async Task SeedLegacyTasksAsync(BpmnDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("INSERT INTO EngineDeployments (Id,Name,CreatedAt) VALUES ('11111111-1111-1111-1111-111111111111','legacy-hash', '2026-01-01'); INSERT INTO ProcessDefinitions (Id,BpmnXml,CreatedAt,DeploymentId,Key,Name,Version) VALUES ('22222222-2222-2222-2222-222222222222','<definitions/>','2026-01-01','11111111-1111-1111-1111-111111111111','legacy-hash','Legacy',1); INSERT INTO ProcessInstances (Id,ActiveTasks,ActiveTokens,CreatedAt,InstanceId,LastModified,ProcessDefinitionId,ProcessId,StartedAt,State,Status,Variables) VALUES ('33333333-3333-3333-3333-333333333333','[]','[]','2026-01-01','legacy-hash','2026-01-01','22222222-2222-2222-2222-222222222222','legacy-hash','2026-01-01','Running',0,'{{}}')", TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO FormDefinitions (Id,TenantId,Key,Name,Schema,Version,CreatedAt,LastModified) VALUES ('legacy-hash-form','tenant-a','approval','Approval',{CurrentSchema},7,'2026-01-01','2026-01-02')", TestContext.Current.CancellationToken);
        await InsertTaskAsync(db, "approval", FrozenSchema);
        await InsertTaskAsync(db, "empty", string.Empty);
        await InsertTaskAsync(db, "no-schema", null);
    }

    private static Task<int> InsertTaskAsync(BpmnDbContext db, string formKey, string? schema)
        => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Tasks (Id,ProcessInstanceId,Name,Type,TenantId,CreatedAt,LastModified,ModifiedBy,Status,CandidateUsers,CandidateRole,RequiredFields,LocalVariables,FormKey,FormSchema) VALUES ({Guid.NewGuid()},'33333333-3333-3333-3333-333333333333','legacy-hash-test','userTask','tenant-a','2026-01-01','2026-01-01','migration-test',0,'[]','','[]',{EmptyObject},{formKey},{schema})", TestContext.Current.CancellationToken);

    private static string Hash(string schema)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(schema))).ToLowerInvariant();
}
