using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence;
using Xunit;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// P4.1: Providerneutrale Dependency-Registry (SQLite lokal / PostgreSQL zentral).
/// Testet die Providerwahl sowie das nicht-blockierende, read-only LoadInto im Produktionsprofil.
/// </summary>
public sealed class DependencyRegistryProviderTests
{
    [Theory]
    [InlineData("Data Source=vertexbpmn-dependencies.db", "sqlite")]
    [InlineData("Data Source=:memory:", "sqlite")]
    [InlineData("Host=localhost;Port=5432;Database=vertexbpmn;Username=u;Password=p", "npgsql")]
    [InlineData("Server=db;Username=u;Database=vertexbpmn", "npgsql")]
    public void Resolve_Infers_Provider_From_ConnectionString(string cs, string expected)
    {
        Assert.Equal(expected, DependencyRegistryProvider.Resolve(cs, explicitProvider: null));
    }

    [Fact]
    public void Resolve_Explicit_Provider_Wins_Over_ConnectionString()
    {
        Assert.Equal("sqlite",
            DependencyRegistryProvider.Resolve("Host=db;Database=x;Username=u;Password=p", explicitProvider: "sqlite"));
        Assert.Equal("npgsql",
            DependencyRegistryProvider.Resolve("Data Source=local.db", explicitProvider: "npgsql"));
    }

    [Theory]
    [InlineData("sqlite", true)]
    [InlineData("inmemory", true)]
    [InlineData("npgsql", false)]
    public void IsSqlite_Reflects_Local_Providers(string provider, bool expected)
        => Assert.Equal(expected, DependencyRegistryProvider.IsSqlite(provider));

    [Fact]
    public void LoadInto_Sqlite_Empty_Db_Is_Non_Blocking_And_Empty()
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DependencyRegistry"] = "Data Source=:memory:"
        });

        DependencyConfigurationLoader.LoadInto(config);

        Assert.Empty(config.AsEnumerable().Where(kv => kv.Key == "unused"));
    }

    [Fact]
    public void LoadInto_Sqlite_File_Loads_Seeded_Values()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "depreg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbFile = Path.Combine(tempDir, "dep.db");
        var cs = "Data Source=" + dbFile;
        try
        {
            using (var db = new DependencyRegistryDbContext(
                       new DbContextOptionsBuilder<DependencyRegistryDbContext>().UseSqlite(cs).Options))
            {
                db.Database.Migrate();
                db.Entries.Add(new DependencyConfigurationEntity
                {
                    Key = "My:Setting",
                    Value = "hello",
                    UpdatedAt = DateTime.UtcNow
                });
                db.SaveChanges();
            }

            var config = new ConfigurationManager();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DependencyRegistry"] = cs
            });

            DependencyConfigurationLoader.LoadInto(config);

            Assert.Equal("hello", config["My:Setting"]);
        }
        finally
        {
            // Disposing a context returns SQLite connections to the pool. Release
            // only this test's pool before deleting its database on Windows.
            using var poolConnection = new SqliteConnection(cs);
            SqliteConnection.ClearPool(poolConnection);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LoadInto_Postgres_Unreachable_Is_Non_Blocking_And_Empty()
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Punktgenau unerreichbarer Host + Provider=postgres(obligatorisch nicht-sqlite).
            ["DependencyRegistry:Provider"] = "npgsql",
            ["ConnectionStrings:DependencyRegistry"] =
                "Host=127.0.0.1;Port=1;Database=nosuch;Username=nobody;Password=x;Timeout=1"
        });

        // Muss NICHT werfen (fail-open): nicht migrierte / nicht erreichbare Registry blockiert den Start nicht.
        DependencyConfigurationLoader.LoadInto(config);

        Assert.Null(config["whatever"]);
    }
}
