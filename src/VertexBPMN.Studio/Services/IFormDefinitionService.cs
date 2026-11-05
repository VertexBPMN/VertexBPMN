namespace VertexBPMN.Studio.Services;

internal interface IFormDefinitionService
{
    Task<IReadOnlyList<StudioFormDefinition>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<StudioFormDefinition?> GetAsync(string id, string tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudioFormRevision>> GetRevisionsAsync(string id, string tenantId, CancellationToken cancellationToken = default);
    Task<StudioFormRevision?> GetRevisionAsync(string id, int version, string tenantId, CancellationToken cancellationToken = default);
    Task<StudioFormDefinition> CreateAsync(StudioFormWriteRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(string id, StudioFormWriteRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, string tenantId, CancellationToken cancellationToken = default);
}
