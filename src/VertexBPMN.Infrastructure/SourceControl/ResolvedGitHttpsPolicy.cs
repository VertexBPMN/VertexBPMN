using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record ResolvedGitHttpsPolicy(GitHttpsNetworkPolicy Network, GitHttpsTrustProfile? Trust)
{
	internal bool Matches(ResolvedGitHttpsPolicy other) =>
		Network with { AllowedAddressRanges = other.Network.AllowedAddressRanges, DeniedAddressRanges = other.Network.DeniedAddressRanges } == other.Network
		&& Network.RepositoryScope.AbsoluteUri == other.Network.RepositoryScope.AbsoluteUri
		&& Network.AllowedAddressRanges.SequenceEqual(other.Network.AllowedAddressRanges, StringComparer.Ordinal)
		&& Network.DeniedAddressRanges.SequenceEqual(other.Network.DeniedAddressRanges, StringComparer.Ordinal)
		&& Trust == other.Trust && Trust?.RepositoryScope.AbsoluteUri == other.Trust?.RepositoryScope.AbsoluteUri;
}
