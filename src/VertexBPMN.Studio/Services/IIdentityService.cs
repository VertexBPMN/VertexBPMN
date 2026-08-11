namespace VertexBPMN.Studio.Services;

public interface IIdentityService
{
    Task<IReadOnlyList<StudioTenant>> ListTenantsAsync(CancellationToken cancellationToken = default);
    Task<StudioTenant> CreateTenantAsync(string name, string? description = null, CancellationToken cancellationToken = default);
    Task UpdateTenantAsync(string id, string name, string? description = null, CancellationToken cancellationToken = default);
    Task DeleteTenantAsync(string id, CancellationToken cancellationToken = default);
}

public sealed record StudioTenant(string Id, string Name, string? Description = null);
