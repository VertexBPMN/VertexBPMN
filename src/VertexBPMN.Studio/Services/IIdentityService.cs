namespace VertexBPMN.Studio.Services;

internal interface IIdentityService
{
    Task<IReadOnlyList<StudioTenant>> ListTenantsAsync(CancellationToken cancellationToken = default);
    Task<StudioTenant> CreateTenantAsync(string name, string? description = null, CancellationToken cancellationToken = default);
    Task UpdateTenantAsync(string id, string name, string? description = null, CancellationToken cancellationToken = default);
    Task DeleteTenantAsync(string id, CancellationToken cancellationToken = default);
}
