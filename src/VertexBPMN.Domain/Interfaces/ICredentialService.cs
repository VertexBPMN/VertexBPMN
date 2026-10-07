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
