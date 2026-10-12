using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>References resolve only against current deployment configuration, never request-provided policy data.</summary>
internal sealed class GitHttpsPolicyRegistry(IOptionsMonitor<SourceControlOptions> options)
{
	internal ResolvedGitHttpsPolicy Resolve(SourceControlContext context, RepositoryBinding binding, RepositoryConnectionProfile profile)
		=> Resolve(context, binding, profile, options.CurrentValue);

	internal static ResolvedGitHttpsPolicy Resolve(SourceControlContext context, RepositoryBinding binding,
		RepositoryConnectionProfile profile, SourceControlOptions configuration)
	{
		if (!configuration.Enabled)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.Disabled);
		}
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding, profile);
		GitHttpsConfigurationPolicy.Validate(configuration);
		var policy = configuration.HttpsNetworkPolicies.SingleOrDefault(candidate => candidate.TenantId == context.TenantId
			&& candidate.Id == profile.GitEndpoint.NetworkPolicyReference && !candidate.IsRevoked)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		var network = GitHttpsConfigurationPolicy.Snapshot(policy);
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding,
			profile with { GitEndpoint = new(network.RepositoryScope, network.Id) });
		GitHttpsTrustProfile? trust = null;
		if (profile.GitEndpoint.TrustProfileReference is { } reference)
		{
			var configured = configuration.HttpsTrustProfiles.SingleOrDefault(candidate => candidate.TenantId == context.TenantId
				&& candidate.Id == reference && !candidate.IsRevoked)
				?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
			trust = GitHttpsConfigurationPolicy.Snapshot(configured);
			RepositoryConnectionProfilePolicy.DemandRepository(context, binding,
				profile with { GitEndpoint = new(trust.RepositoryScope, network.Id, trust.Id) });
		}
		return new(network, trust);
	}

	internal void DemandCurrent(SourceControlContext context, RepositoryBinding binding, ApprovedGitHttpsTarget approved)
	{
		var accepted = approved.AdministrativePolicy
			?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		DemandCurrent(context, binding, approved.Profile, accepted);
	}

	internal void DemandCurrent(SourceControlContext context, RepositoryBinding binding, RepositoryConnectionProfile profile,
		ResolvedGitHttpsPolicy accepted)
	{
		if (!accepted.Matches(Resolve(context, binding, profile)))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
	}
}
