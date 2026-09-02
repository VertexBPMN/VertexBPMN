namespace VertexBPMN.Application.Configuration;

public sealed record DependencyConfigurationEntry(string Key, string Value, DateTime UpdatedAt);

public interface IDependencyRegistry
{
    Task<IReadOnlyList<DependencyConfigurationEntry>> ListAsync(CancellationToken cancellationToken = default);
    Task<DependencyConfigurationEntry?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
}
