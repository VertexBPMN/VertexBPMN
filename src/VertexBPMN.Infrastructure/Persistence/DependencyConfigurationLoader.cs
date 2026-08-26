using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace VertexBPMN.Infrastructure.Persistence;

public static class DependencyConfigurationLoader
{
    public static void LoadInto(IConfigurationManager configuration)
    {
        var connectionString = ResolveConnectionString(configuration);
        var options = new DbContextOptionsBuilder<DependencyRegistryDbContext>()
            .UseSqlite(connectionString)
            .Options;

        using var dbContext = new DependencyRegistryDbContext(options);
        dbContext.Database.Migrate();
        var values = dbContext.Entries
            .AsNoTracking()
            .ToDictionary(entry => entry.Key, entry => (string?)entry.Value, StringComparer.OrdinalIgnoreCase);

        if (values.Count > 0)
            configuration.AddInMemoryCollection(values);
    }

    public static string ResolveConnectionString(IConfiguration configuration)
    {
        return configuration.GetConnectionString("DependencyRegistry")
            ?? configuration["DependencyRegistry:ConnectionString"]
            ?? "Data Source=vertexbpmn.db";
    }
}
