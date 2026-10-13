using System.Net;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal static class GitSshNetworkBoundary
{
	internal static async Task<ApprovedGitSshTarget> ResolveAsync(SourceControlContext context, RepositoryBinding binding,
		SourceControlExecutionTarget target, GitSshNetworkPolicy network, GitSshTrustProfile trust,
		Func<string, CancellationToken, Task<IPAddress[]>> resolve, CancellationToken cancellationToken)
	{
		network = GitSshConfigurationPolicy.Snapshot(network);
		trust = GitSshConfigurationPolicy.Snapshot(trust);
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding, target.Profile);
		if (target.Remote.AbsoluteUri != binding.Remote.AbsoluteUri || binding.ConnectionProfileId != target.Profile.Id
			|| network.IsRevoked || trust.IsRevoked || network.TenantId != context.TenantId || trust.TenantId != context.TenantId
			|| network.Id != target.Profile.GitEndpoint.NetworkPolicyReference || trust.Id != target.Profile.GitEndpoint.TrustProfileReference)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable); }
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding, target.Profile with { GitEndpoint = new(network.RepositoryScope, network.Id, trust.Id) });
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding, target.Profile with { GitEndpoint = new(trust.RepositoryScope, network.Id, trust.Id) });
		cancellationToken.ThrowIfCancellationRequested();
		var addresses = await resolve(binding.Remote.IdnHost, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		GitHttpsNetworkBoundary.DemandAddresses(addresses, network.AllowedAddressRanges, network.DeniedAddressRanges);
		return new(target, network, trust, addresses.Distinct().ToArray());
	}
}
