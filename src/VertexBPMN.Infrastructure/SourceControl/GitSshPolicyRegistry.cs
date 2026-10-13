using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Resolve deployment-owned SSH rules; request payloads cannot supply trust or network approval.</summary>
internal sealed class GitSshPolicyRegistry(IOptionsMonitor<SourceControlOptions> options)
{
	internal ResolvedGitSshPolicy Resolve(SourceControlContext context, RepositoryBinding binding, RepositoryConnectionProfile profile)
	{
		var configuration = options.CurrentValue;
		if (!configuration.Enabled)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.Disabled);
		}
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding, profile);
		if (profile.Transport != GitTransportKind.Ssh || profile.Authentication != GitAuthenticationKind.SshKey)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		GitSshConfigurationPolicy.Validate(configuration);
		GitSshConfigurationPolicy.ValidateScope(binding.Remote);
		var network = configuration.SshNetworkPolicies.SingleOrDefault(candidate => candidate.TenantId == context.TenantId
			&& candidate.Id == profile.GitEndpoint.NetworkPolicyReference && !candidate.IsRevoked)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		var trust = configuration.SshTrustProfiles.SingleOrDefault(candidate => candidate.TenantId == context.TenantId
			&& candidate.Id == profile.GitEndpoint.TrustProfileReference && !candidate.IsRevoked)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		network = GitSshConfigurationPolicy.Snapshot(network);
		trust = GitSshConfigurationPolicy.Snapshot(trust);
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding,
			profile with { GitEndpoint = new(network.RepositoryScope, network.Id, trust.Id) });
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding,
			profile with { GitEndpoint = new(trust.RepositoryScope, network.Id, trust.Id) });
		return new(network, trust);
	}

	internal void DemandCurrent(SourceControlContext context, RepositoryBinding binding, RepositoryConnectionProfile profile,
		ResolvedGitSshPolicy accepted)
	{
		if (!accepted.Matches(Resolve(context, binding, profile)))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
	}
}
