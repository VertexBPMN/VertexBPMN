namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>
/// Hosting identity is scoped to a provider, API endpoint and repository. The adapter
/// must verify its relationship to the Git remote before any credentials are released.
/// </summary>
public sealed record RepositoryHostingConnection(string ProviderId, RepositoryEndpoint ApiEndpoint,
	string RepositoryId, string CredentialReference, SourceControlCapability Capabilities);
