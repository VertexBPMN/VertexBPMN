using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Infrastructure;

public sealed class SharedSqliteDbFixture : IAsyncLifetime
{
    private bool _initialized;
    private readonly object _lock = new();

    public SqliteConnection Connection { get; } =
        new("Data Source=:memory:;Mode=Memory;Cache=Shared");

    public DbContextOptions<BpmnDbContext> BpmnOptions =>
        new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(Connection).Options;

    public DbContextOptions<TenantDbContext> TenantOptions =>
        new DbContextOptionsBuilder<TenantDbContext>().UseSqlite(Connection).Options;

    public DbContextOptions<SimulationScenarioDbContext> SimulationOptions =>
        new DbContextOptionsBuilder<SimulationScenarioDbContext>().UseSqlite(Connection).Options;
    public DbContextOptions<DecisionDbContext> DecisionOptions =>
        new DbContextOptionsBuilder<DecisionDbContext>().UseSqlite(Connection).Options;
    public DbContextOptions<ProcessMiningEventDbContext> ProcessMiningOptions =>
        new DbContextOptionsBuilder<ProcessMiningEventDbContext>().UseSqlite(Connection).Options;

    // Add other context options (DecisionDbContext, ProcessMiningEventDbContext, etc.) as required.

    public async ValueTask InitializeAsync()
    {
        Connection.Open(); // Must stay open for :memory: lifetime.


        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;
            ExecuteSchemaAndSeed();
            _initialized = true;
        }
        await Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        // Keep connection open until test run ends (xUnit disposes fixture after collection).
        return ValueTask.CompletedTask;
    }

    private void ExecuteSchemaAndSeed()
    {
        // Load the SQL script (adjust path if tests run with different base directory).
        // We rely on relative path from test bin to project root.
        var root = GetSolutionRoot();
        var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Sqlite.sql");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("Sqlite.sql not found", scriptPath);

        var raw = File.ReadAllText(scriptPath);

        // Simple splitter (SQLite doesn’t support GO). We remove comments and execute batches separated by ';'.
        var batches = SplitSqlBatches(raw);

        using var cmd = Connection.CreateCommand();
        foreach (var batch in batches)
        {
            cmd.CommandText = batch;
            cmd.ExecuteNonQuery();
        }
    }

    private static IEnumerable<string> SplitSqlBatches(string sql)
    {
        var sb = new StringBuilder();
        foreach (var line in sql.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("--")) continue; // skip line comments

            sb.AppendLine(line);

            if (trimmed.EndsWith(";"))
            {
                var statement = sb.ToString().Trim();
                sb.Clear();
                if (statement.Length > 1)
                    yield return statement;
            }
        }
        // Last chunk if missing final semicolon
        if (sb.Length > 0)
        {
            var remaining = sb.ToString().Trim();
            if (remaining.Length > 0)
                yield return remaining;
        }
    }

    private static string GetSolutionRoot()
    {
        // Walk up until we find the .sln (simple heuristic).
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.GetFiles(dir, "*.sln").Any())
        {
            var parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }
        return dir ?? AppContext.BaseDirectory;
    }
}