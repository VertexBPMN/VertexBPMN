namespace VertexBPMN.Studio.Services;

internal interface ICredentialService
{
    Task<IReadOnlyList<StudioCredential>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<StudioCredential> CreateAsync(string tenantId, string name, string type, string? description, string key, string value, CancellationToken cancellationToken = default);
    Task UpdateMetadataAsync(string tenantId, string id, string name, string type, string? description, CancellationToken cancellationToken = default);
    Task RotateSecretAsync(string tenantId, string id, string key, string value, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
    Task<string> StartOAuth2AuthorizationAsync(string tenantId, string credentialId, OAuth2ConnectConfig config, string? browserProof = null, CancellationToken cancellationToken = default);
    Task CompleteOAuth2AuthorizationAsync(string state, string code, string browserProof, CancellationToken cancellationToken = default);
}
