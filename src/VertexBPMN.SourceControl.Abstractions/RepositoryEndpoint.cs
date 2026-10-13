namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>
/// Administrative endpoint scope. References identify server-owned network and trust
/// policies, never CIDRs, certificates, executable paths or credentials from a request.
/// </summary>
public sealed record RepositoryEndpoint(Uri BaseUri, string NetworkPolicyReference,
	string? TrustProfileReference = null);
