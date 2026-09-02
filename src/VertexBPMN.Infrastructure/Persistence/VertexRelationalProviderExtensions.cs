using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace VertexBPMN.Infrastructure.Persistence;

/// <summary>
/// Configures providers that execute VertexBPMN's shared, SQLite-authored
/// migration chain. Provider-specific store-type annotations make EF report a
/// false model-drift warning even when the canonical model snapshot is current.
/// The canonical SQLite model remains strict; PostgreSQL and SQL Server prove
/// compatibility by applying the complete migration chain in acceptance tests.
/// </summary>
public static class VertexRelationalProviderExtensions
{
    public static DbContextOptionsBuilder UseVertexNpgsql(
        this DbContextOptionsBuilder options,
        string connectionString) =>
        ConfigureSharedMigrations(options.UseNpgsql(connectionString));

    public static DbContextOptionsBuilder<TContext> UseVertexNpgsql<TContext>(
        this DbContextOptionsBuilder<TContext> options,
        string connectionString)
        where TContext : DbContext
    {
        options.UseNpgsql(connectionString);
        ConfigureSharedMigrations(options);
        return options;
    }

    public static DbContextOptionsBuilder UseVertexSqlServer(
        this DbContextOptionsBuilder options,
        string connectionString) =>
        ConfigureSharedMigrations(options.UseSqlServer(connectionString));

    public static DbContextOptionsBuilder<TContext> UseVertexSqlServer<TContext>(
        this DbContextOptionsBuilder<TContext> options,
        string connectionString)
        where TContext : DbContext
    {
        options.UseSqlServer(connectionString);
        ConfigureSharedMigrations(options);
        return options;
    }

    private static TBuilder ConfigureSharedMigrations<TBuilder>(TBuilder options)
        where TBuilder : DbContextOptionsBuilder
    {
        options.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        return options;
    }
}
