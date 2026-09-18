using Microsoft.EntityFrameworkCore;

namespace VertexBPMN.Studio.Services;

/// <summary>
/// Providerwahl für den geteilten OIDC-Session-Store (P4.5): SQLite lokal,
/// PostgreSQL (Npgsql) in Azure. Die Wahl erfolgt über <c>OidcSessionStore:Provider</c>
/// ("npgsql" | "sqlite") bzw. wird aus dem Connection-String inferiert.
/// </summary>
public static class OidcSessionStoreProvider
{
    public static string Resolve(string? connectionString, string? explicitProvider)
    {
        var provider = string.IsNullOrWhiteSpace(explicitProvider) ? null : explicitProvider!.Trim().ToLowerInvariant();
        return provider ?? InferFromConnectionString(connectionString);
    }

    public static bool IsSqlite(string provider) => provider is "sqlite" or "inmemory";

    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string provider, string connectionString)
    {
        if (IsSqlite(provider))
            options.UseSqlite(connectionString);
        else
            options.UseNpgsql(connectionString);
        return options;
    }

    public static DbContextOptionsBuilder<TContext> Configure<TContext>(
        DbContextOptionsBuilder<TContext> options,
        string provider,
        string connectionString)
        where TContext : DbContext
    {
        if (IsSqlite(provider))
            options.UseSqlite(connectionString);
        else
            options.UseNpgsql(connectionString);
        return options;
    }

    private static string InferFromConnectionString(string? cs)
    {
        if (string.IsNullOrWhiteSpace(cs))
            return "sqlite";

        var lower = cs.ToLowerInvariant();
        if (lower.Contains("host=") || (lower.Contains("username=") && lower.Contains("database=")))
            return "npgsql";

        return "sqlite";
    }
}
