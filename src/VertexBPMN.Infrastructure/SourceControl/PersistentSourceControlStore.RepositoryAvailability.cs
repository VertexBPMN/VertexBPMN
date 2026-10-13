using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	public async Task<SourceControlAvailability> GetRepositoryAvailabilityAsync(SourceControlContext context, Guid repositoryId,
		IReadOnlyCollection<string> roles, IOptionsMonitor<SourceControlOptions> configuration, CancellationToken cancellationToken)
	{
		var currentRoles = roles.ToArray();
		var access = await FindAsync(context.TenantId, repositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, currentRoles, access.Grants, RepositoryPermission.Read);
		var capabilities = SourceControlCapability.Read;
		if (access.Binding.ConnectionProfileId is null)
		{
			if (!string.IsNullOrWhiteSpace(configuration.CurrentValue.IdentityCredentialReference))
			{
				if (HasRepositoryPermission(context, access, currentRoles, RepositoryPermission.Commit))
				{
					capabilities |= SourceControlCapability.Commit;
				}
				if (HasRepositoryPermission(context, access, currentRoles, RepositoryPermission.Push))
				{
					capabilities |= SourceControlCapability.Push;
				}
				if (HasRepositoryPermission(context, access, currentRoles, RepositoryPermission.PullRequest))
				{
					capabilities |= SourceControlCapability.PullRequest;
				}
			}
			return new(true, capabilities, null);
		}
		try
		{
			var target = await CaptureConnectionTargetAsync(context, repositoryId, currentRoles, SourceControlOperationKind.Read, cancellationToken);
			if (target.Profile.Transport == GitTransportKind.Ssh)
			{
				return SshReadAvailability(context, access.Binding, target.Profile, configuration);
			}
			if (target.Profile.Transport != GitTransportKind.Https
				|| target.Profile.Authentication is not (GitAuthenticationKind.HttpBasic or GitAuthenticationKind.HttpBearer or GitAuthenticationKind.Anonymous))
			{
				return new(false, SourceControlCapability.None, SourceControlErrorCode.ProviderUnavailable);
			}
			_ = new GitHttpsPolicyRegistry(configuration).Resolve(context, access.Binding, target.Profile);
			if (target.Profile.Authentication is GitAuthenticationKind.HttpBasic or GitAuthenticationKind.HttpBearer)
			{
				capabilities |= ProfiledWriteCapabilities(context, access, currentRoles, target.Profile);
			}
			if (HasRepositoryPermission(context, access, currentRoles, RepositoryPermission.PullRequest))
			{
				try
				{
					_ = await CaptureHostingExecutionAsync(context, access, currentRoles, cancellationToken);
					capabilities |= SourceControlCapability.PullRequest;
				}
				catch (SourceControlSecurityException)
				{
					// Optional hosting configuration never removes otherwise valid Git capabilities.
				}
			}
			return new(true, capabilities, null);
		}
		catch (SourceControlSecurityException error)
		{
			return new(false, SourceControlCapability.None, error.Code);
		}
	}

	private static SourceControlCapability ProfiledWriteCapabilities(SourceControlContext context, RepositoryAccessSnapshot access,
		IReadOnlyCollection<string> roles, RepositoryConnectionProfile profile)
	{
		var capabilities = SourceControlCapability.None;
		if (profile.GitCapabilities.HasFlag(SourceControlCapability.Commit)
			&& HasRepositoryPermission(context, access, roles, RepositoryPermission.Commit))
		{
			capabilities |= SourceControlCapability.Commit;
		}
		if (profile.GitCapabilities.HasFlag(SourceControlCapability.Push)
			&& HasRepositoryPermission(context, access, roles, RepositoryPermission.Push))
		{
			capabilities |= SourceControlCapability.Push;
		}
		return capabilities;
	}

	private static SourceControlAvailability SshReadAvailability(SourceControlContext context, RepositoryBinding binding,
		RepositoryConnectionProfile profile, IOptionsMonitor<SourceControlOptions> configuration)
	{
		_ = new GitSshPolicyRegistry(configuration).Resolve(context, binding, profile);
		return File.Exists(configuration.CurrentValue.SshExecutablePath)
			? new(true, SourceControlCapability.Read, null)
			: new(false, SourceControlCapability.None, SourceControlErrorCode.GitUnavailable);
	}

	private static bool HasRepositoryPermission(SourceControlContext context, RepositoryAccessSnapshot access,
		IReadOnlyCollection<string> roles, RepositoryPermission permission)
	{
		try
		{
			RepositoryAccessPolicy.Demand(context, access.Binding, roles, access.Grants, permission);
			return true;
		}
		catch (SourceControlSecurityException)
		{
			return false;
		}
	}
}
