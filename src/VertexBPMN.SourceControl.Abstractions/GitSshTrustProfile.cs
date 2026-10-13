namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Administrator-pinned host public keys (algorithm and Base64), never user-provided known_hosts text.</summary>
public sealed record GitSshTrustProfile(string Id, string TenantId, Uri RepositoryScope, IReadOnlyList<string> HostKeys)
{
	public long Revision { get; init; } = 1;
	public bool IsRevoked { get; init; }
}
