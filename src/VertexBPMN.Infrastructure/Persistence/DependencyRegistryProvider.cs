using Microsoft.EntityFrameworkCore;

namespace VertexBPMN.Infrastructure.Persistence;

/// <summary>
/// Providerwahl für die Dependency-Registry (P4.1): zusätzlich zu SQLite (lokal) wird PostgreSQL als
/// zentraler, multi-host-fähiger Provider unterstützt. Die Wahl erfolgt entweder explizit über
/// <c>DependencyRegistry:Provider</c> ("npgsql" | "sqlite") oder wird aus dem Connection-String inferiert.
/// SQLite bleibt der lokale Entwicklungsfallback; PostgreSQL ist das Azure-Produktionsziel.
/// </summary>
public static class DependencyRegistryProvider
{
    /// <summary>Auflösen des Providers aus expliziter Konfiguration bzw. Connection-String.</summary>
    public static string Resolve(string? connectionString, string? explicitProvider)
    {
        var provider = string.IsNullOrWhiteSpace(explicitProvider) ? null : explicitProvider!.Trim().ToLowerInvariant();
        return provider ?? InferFromConnectionString(connectionString);
    }

    /// <summary>Wahr, wenn der Provider lokal (kein zentraler Store) ist.</summary>
    public static bool IsSqlite(string provider) => provider is "sqlite" or "inmemory";

    /// <summary>Konfiguriert den DbContext für den aufgelösten Provider (SQLite lokal, sonst Npgsql).</summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string provider, string connectionString)
    {
        if (IsSqlite(provider))
            options.UseSqlite(connectionString);
        else
            options.UseVertexNpgsql(connectionString);
        return options;
    }

    private static string InferFromConnectionString(string? cs)
    {
        if (string.IsNullOrWhiteSpace(cs))
            return "sqlite";

        var lower = cs.ToLowerInvariant();
        // PostgreSQL: Host=... bzw. Username=... mit Database=...
        if (lower.Contains("host=") || (lower.Contains("username=") && lower.Contains("database=")))
            return "npgsql";

        // Standard-Rückfall: lokale SQLite-Datei (z. B. "Data Source=vertexbpmn-dependencies.db").
        return "sqlite";
    }
}
