namespace VertexBPMN.SourceControl.Abstractions;

public sealed record GitSshNetworkPolicy(string Id, string TenantId, Uri RepositoryScope,
	IReadOnlyList<string> AllowedAddressRanges, IReadOnlyList<string> DeniedAddressRanges)
{
	public long Revision { get; init; } = 1;
	public bool IsRevoked { get; init; }
}
