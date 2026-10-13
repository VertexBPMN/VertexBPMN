using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ControlledGitProcess
{
	// Prepared transport boundary. The worker must supply freshly authorized, server-resolved metadata.
	internal async Task FetchProfileRevisionAsync(GitWorkspace workspace, SourceControlContext context,
		RepositoryBinding binding, RepositoryConnectionProfile profile, GitHttpsNetworkPolicy networkPolicy,
		GitCommitId revision, IGitCredentialLease? lease, CancellationToken cancellationToken)
	{
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding, profile);
		if (profile.Authentication is not (GitAuthenticationKind.Anonymous or GitAuthenticationKind.HttpBasic))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}

		if (binding.ConnectionProfileId != profile.Id || profile.Transport != GitTransportKind.Https
			|| (profile.GitCapabilities & SourceControlCapability.Read) == SourceControlCapability.None
			|| profile.GitEndpoint.TrustProfileReference is not null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}

		var authenticationMatches = profile.Authentication switch
		{
			GitAuthenticationKind.Anonymous => lease is null,
			GitAuthenticationKind.HttpBasic => lease is GitHttpBasicCredentialLease,
			_ => false
		};
		if (!authenticationMatches)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}

		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.WriteTimeout);
		try
		{
			var target = await GitHttpsNetworkBoundary.ResolveAsync(context, binding, profile, networkPolicy, deadline.Token);
			await VersionAsync(workspace, deadline.Token);
			await FetchCoreAsync(workspace, target.Remote, "", lease, target.CopyAddresses(), null, deadline.Token, revision);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
		}
	}
}
