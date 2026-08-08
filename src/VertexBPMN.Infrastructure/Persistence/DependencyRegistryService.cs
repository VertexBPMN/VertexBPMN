using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.Configuration;

namespace VertexBPMN.Infrastructure.Persistence;

public sealed class DependencyRegistryService(DependencyRegistryDbContext dbContext) : IDependencyRegistry
{
    public async Task<IReadOnlyList<DependencyConfigurationEntry>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Entries
            .AsNoTracking()
            .OrderBy(entry => entry.Key)
            .Select(entry => new DependencyConfigurationEntry(entry.Key, entry.Value, entry.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<DependencyConfigurationEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.Entries.AsNoTracking().SingleOrDefaultAsync(entry => entry.Key == key, cancellationToken);
        return entry is null ? null : new DependencyConfigurationEntry(entry.Key, entry.Value, entry.UpdatedAt);
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        var entry = await dbContext.Entries.SingleOrDefaultAsync(candidate => candidate.Key == key, cancellationToken);
        if (entry is null)
        {
            dbContext.Entries.Add(new DependencyConfigurationEntity
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            entry.Value = value;
            entry.UpdatedAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.Entries.SingleOrDefaultAsync(candidate => candidate.Key == key, cancellationToken);
        if (entry is null)
            return false;

        dbContext.Entries.Remove(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Any(char.IsWhiteSpace))
            throw new ArgumentException("Configuration key must not be empty or contain whitespace.", nameof(key));
    }
}
