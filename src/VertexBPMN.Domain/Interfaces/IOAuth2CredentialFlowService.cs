namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Authorization-Code-OAuth2-Flow für Connector-Credentials vom Typ "oauth2".
/// Persistente Metadaten (State) sind tenant-isoliert; Secrets liegen im verschlüsselten Credential-Store.
/// </summary>
public interface IOAuth2CredentialFlowService
{
	Task<OAuth2AuthorizationStart> StartAuthorizationAsync(
		string tenantId, string credentialId, OAuth2AuthorizationConfig config, OAuth2FlowBinding? binding = null, CancellationToken cancellationToken = default);

	Task<bool> CompleteAuthorizationAsync(
		string state, string code, OAuth2FlowBinding? binding = null, CancellationToken cancellationToken = default);

	Task<string?> ResolveValidAccessTokenAsync(
		string tenantId,
		string credentialId,
		CancellationToken cancellationToken = default);
}
