namespace VertexBPMN.Domain.Interfaces;

public interface ICredentialService
{
    Task<IReadOnlyList<CredentialMetadata>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<CredentialMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default);
    Task<CredentialMetadata> CreateAsync(string tenantId, CredentialWriteRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateMetadataAsync(string tenantId, string id, CredentialMetadataUpdate request, CancellationToken cancellationToken = default);
    Task<bool> RotateSecretAsync(string tenantId, string id, CredentialSecretRotation request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
    Task<string?> ResolveSecretAsync(string tenantId, string id, string key, CancellationToken cancellationToken = default);
}

public sealed record CredentialMetadata(
    string Id,
    string TenantId,
    string Name,
    string Type,
    string? Description,
    IReadOnlyList<string> SecretKeys,
    DateTime CreatedAt,
    DateTime LastModified,
    DateTime? LastUsedAt);

public sealed record CredentialWriteRequest(
    string Name,
    string Type,
    string? Description,
    IReadOnlyDictionary<string, string> Secrets);

public sealed record CredentialMetadataUpdate(string Name, string Type, string? Description);

public sealed record CredentialSecretRotation(string Key, string Value);
