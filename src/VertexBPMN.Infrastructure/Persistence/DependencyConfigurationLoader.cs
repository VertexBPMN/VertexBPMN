using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace VertexBPMN.Infrastructure.Persistence;

public static class DependencyConfigurationLoader
{
    public static void LoadInto(IConfigurationManager configuration, ILogger? logger = null)
    {
        var connectionString = ResolveConnectionString(configuration);
        var provider = DependencyRegistryProvider.Resolve(connectionString, configuration["DependencyRegistry:Provider"]);
        var options = new DbContextOptionsBuilder<DependencyRegistryDbContext>();
        DependencyRegistryProvider.Configure(options, provider, connectionString);

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var dbContext = new DependencyRegistryDbContext(options.Options);
            // SQLite (lokal): automatisch migrieren wie bisher. Für PostgreSQL (Produktion) läuft die
            // Migration ausschließlich über den Migrationspfad; der Loader liest NUR und darf den Start
            // bei noch nicht migrierter / nicht erreichbarer Registry nicht blockieren (P4.1).
            if (DependencyRegistryProvider.IsSqlite(provider))
                dbContext.Database.Migrate();

            foreach (var entry in dbContext.Entries.AsNoTracking())
                values[entry.Key] = entry.Value;

            if (values.Count > 0)
                configuration.AddInMemoryCollection(values);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "Dependency registry read failed for provider '{Provider}'; continuing without registry values.", provider);
        }
    }
    public static string ResolveConnectionString(IConfiguration configuration)
    {
        return configuration.GetConnectionString("DependencyRegistry")
            ?? configuration["DependencyRegistry:ConnectionString"]
            ?? "Data Source=vertexbpmn-dependencies.db";
    }
}
