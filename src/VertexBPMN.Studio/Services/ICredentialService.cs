namespace VertexBPMN.Studio.Services;

public interface ICredentialService
{
    Task<IReadOnlyList<StudioCredential>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<StudioCredential> CreateAsync(string tenantId, string name, string type, string? description, string key, string value, CancellationToken cancellationToken = default);
    Task UpdateMetadataAsync(string tenantId, string id, string name, string type, string? description, CancellationToken cancellationToken = default);
    Task RotateSecretAsync(string tenantId, string id, string key, string value, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}

public sealed record StudioCredential(
    string Id,
    string TenantId,
    string Name,
    string Type,
    string? Description,
    IReadOnlyList<string> SecretKeys,
    DateTime CreatedAt,
    DateTime LastModified,
    DateTime? LastUsedAt);
