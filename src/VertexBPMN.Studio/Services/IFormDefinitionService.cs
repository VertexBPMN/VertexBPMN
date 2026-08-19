namespace VertexBPMN.Studio.Services;

public interface IFormDefinitionService
{
    Task<IReadOnlyList<StudioFormDefinition>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<StudioFormDefinition?> GetAsync(string id, string tenantId, CancellationToken cancellationToken = default);
    Task<StudioFormDefinition> CreateAsync(StudioFormWriteRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(string id, StudioFormWriteRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, string tenantId, CancellationToken cancellationToken = default);
}
public sealed record StudioFormDefinition(string Id, string TenantId, string Key, string Name, string Schema, int Version, DateTime CreatedAt, DateTime LastModified);
public sealed record StudioFormWriteRequest(string TenantId, string Key, string Name, string Schema);
