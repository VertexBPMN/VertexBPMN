using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using VertexBPMN.Infrastructure.Persistence;
using Xunit;

namespace VertexBPMN.Tests.Acceptance;

/// <summary>
/// P4.1: Dependency-Registry auf PostgreSQL Flexible/Server (providerneutrale Registrierung).
/// Beweist gegen einen echten, isolierten PostgreSQL: (a) Migration über den Migrationspfad,
/// (b) registrierte CRUD (gleiche Transaktion), (c) read-only LoadInto ohne Auto-Migration,
/// (d) Transaktion mit Rollback, (e) parallele CLI/API-artige Schreibzugriffe.
/// Läuft ohne VERTEXBPMN_TEST_POSTGRES_ADMIN als Skip.
/// </summary>
public sealed class DependencyRegistryPostgresAcceptanceTests
{
    private const string Category = "PostgresProviderAcceptance";

    private static string AdminConnectionString =>
        Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_POSTGRES_ADMIN") ?? "";

    private static string ConnectionStringFor(string admin, string database)
        => Regex.Replace(admin, @"Database=[^;]*", $"Database={database}", RegexOptions.IgnoreCase);

    private static async Task<string> CreateDatabaseAsync(string admin, string databaseName)
    {
        await using var adminConn = new NpgsqlConnection(admin);
        await adminConn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = adminConn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return ConnectionStringFor(admin, databaseName);
    }

    private static async Task DropDatabaseAsync(string admin, string databaseName)
    {
        await using var adminConn = new NpgsqlConnection(admin);
        await adminConn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = adminConn.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Postgres_Migrates_And_Registry_Crud_Works()
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(AdminConnectionString), "Local PostgreSQL connection required.");
        var db = $"depreg_{Guid.NewGuid():N}";
        var cs = await CreateDatabaseAsync(AdminConnectionString, db);
        try
        {
            // Migration ausschließlich über den Migrationspfad (kein LoadInto).
            await using (var ctx = new DependencyRegistryDbContext(
                             new DbContextOptionsBuilder<DependencyRegistryDbContext>().UseVertexNpgsql(cs).Options))
                await ctx.Database.MigrateAsync(TestContext.Current.CancellationToken);

            await using var scope = new DependencyRegistryDbContext(
                new DbContextOptionsBuilder<DependencyRegistryDbContext>().UseVertexNpgsql(cs).Options);
            var service = new DependencyRegistryService(scope);
            await service.SetAsync("depreg:key", "v1", TestContext.Current.CancellationToken);
            var got = await service.GetAsync("depreg:key", TestContext.Current.CancellationToken);
            Assert.NotNull(got);
            Assert.Equal("v1", got!.Value);

            var list = await service.ListAsync(TestContext.Current.CancellationToken);
            Assert.Contains(list, e => e.Key == "depreg:key" && e.Value == "v1");
        }
        finally
        {
            await DropDatabaseAsync(AdminConnectionString, db);
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Postgres_LoadInto_Reads_Without_Migrating()
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(AdminConnectionString), "Local PostgreSQL connection required.");
        var db = $"depreg_{Guid.NewGuid():N}";
        var cs = await CreateDatabaseAsync(AdminConnectionString, db);
        try
        {
            await using (var ctx = new DependencyRegistryDbContext(
                             new DbContextOptionsBuilder<DependencyRegistryDbContext>().UseVertexNpgsql(cs).Options))
                await ctx.Database.MigrateAsync(TestContext.Current.CancellationToken);

            await using (var scope = new DependencyRegistryDbContext(
                             new DbContextOptionsBuilder<DependencyRegistryDbContext>().UseVertexNpgsql(cs).Options))
            {
                scope.Entries.Add(new DependencyConfigurationEntity { Key = "Dep:Setting", Value = "from-postgres", UpdatedAt = DateTime.UtcNow });
                await scope.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // LoadInto mit Provider=npgsql muss LESEN, ohne zu migrieren (bereits migrierte DB).
            var config = new ConfigurationManager();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DependencyRegistry:Provider"] = "npgsql",
                ["ConnectionStrings:DependencyRegistry"] = cs
            });
            DependencyConfigurationLoader.LoadInto(config);

            Assert.Equal("from-postgres", config["Dep:Setting"]);
        }
        finally
        {
            await DropDatabaseAsync(AdminConnectionString, db);
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Postgres_Transaction_Rollback_Reverts()
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(AdminConnectionString), "Local PostgreSQL connection required.");
        var db = $"depreg_{Guid.NewGuid():N}";
        var cs = await CreateDatabaseAsync(AdminConnectionString, db);
        try
        {
            await using var ctx = new DependencyRegistryDbContext(
                new DbContextOptionsBuilder<DependencyRegistryDbContext>().UseVertexNpgsql(cs).Options);
            await ctx.Database.MigrateAsync(TestContext.Current.CancellationToken);

            await using var tx = await ctx.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            ctx.Entries.Add(new DependencyConfigurationEntity { Key = "tx:key", Value = "rolled-back", UpdatedAt = DateTime.UtcNow });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
            await tx.RollbackAsync(TestContext.Current.CancellationToken);

            Assert.Null(await ctx.Entries.AsNoTracking().SingleOrDefaultAsync(
                e => e.Key == "tx:key", TestContext.Current.CancellationToken));
        }
        finally
        {
            await DropDatabaseAsync(AdminConnectionString, db);
        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Postgres_Parallel_Writes_Distinct_Keys_All_Land()
    {
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(AdminConnectionString), "Local PostgreSQL connection required.");
        var db = $"depreg_{Guid.NewGuid():N}";
        var cs = await CreateDatabaseAsync(AdminConnectionString, db);
        try
        {
            await using (var ctx = new DependencyRegistryDbContext(
                             new DbContextOptionsBuilder<DependencyRegistryDbContext>().UseVertexNpgsql(cs).Options))
                await ctx.Database.MigrateAsync(TestContext.Current.CancellationToken);

            const int n = 8;
            var tasks = Enumerable.Range(0, n).Select(async i =>
            {
                await using var scope = new DependencyRegistryDbContext(
                    new DbContextOptionsBuilder<DependencyRegistryDbContext>().UseVertexNpgsql(cs).Options);
                var service = new DependencyRegistryService(scope);
                await service.SetAsync($"par:{i}", $"val-{i}", TestContext.Current.CancellationToken);
            });
            await Task.WhenAll(tasks);

            await using var read = new DependencyRegistryDbContext(
                new DbContextOptionsBuilder<DependencyRegistryDbContext>().UseVertexNpgsql(cs).Options);
            var keys = await read.Entries.AsNoTracking().Select(e => e.Key).ToListAsync(TestContext.Current.CancellationToken);
            Assert.Equal(n, keys.Count(k => k.StartsWith("par:", StringComparison.Ordinal)));
        }
        finally
        {
            await DropDatabaseAsync(AdminConnectionString, db);
        }
    }
}
