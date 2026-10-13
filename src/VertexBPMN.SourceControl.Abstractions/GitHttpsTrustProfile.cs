namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Server-owned public CA bundle, scoped to a tenant and repository endpoint; never private keys.</summary>
public sealed record GitHttpsTrustProfile(string Id, string TenantId, Uri RepositoryScope, string CertificateAuthorityPem)
{
	public long Revision { get; init; } = 1;
	public bool IsRevoked { get; init; }
}
