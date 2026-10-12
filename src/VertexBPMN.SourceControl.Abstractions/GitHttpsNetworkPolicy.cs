namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Trusted administrative configuration, never supplied by a repository request.</summary>
public sealed record GitHttpsNetworkPolicy(string Id, string TenantId, Uri RepositoryScope,
	IReadOnlyList<string>? AllowedAddressRanges = null, IReadOnlyList<string>? DeniedAddressRanges = null)
{
	public IReadOnlyList<string> AllowedAddressRanges { get; init; } = AllowedAddressRanges ?? [];
	public IReadOnlyList<string> DeniedAddressRanges { get; init; } = DeniedAddressRanges ?? [];
	public long Revision { get; init; } = 1;
	public bool IsRevoked { get; init; }
}
