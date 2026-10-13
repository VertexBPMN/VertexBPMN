using System.Net;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Prepared orchestration: authorize, approve one DNS snapshot, resolve credentials, then use that same snapshot.</summary>
internal sealed partial class ProfiledGitHttpsTransport(PersistentSourceControlStore store, ISourceControlActorResolver actors,
	GitHttpsPolicyRegistry policies, ProfiledGitCredentialBroker credentials, ControlledGitProcess git, IOptions<SourceControlOptions> options)
{
	internal Task<ApprovedGitHttpsTarget> PrepareAsync(SourceControlContext context, SourceControlExecutionTarget target,
		CancellationToken cancellationToken) => PrepareAsync(context, target, Dns.GetHostAddressesAsync, cancellationToken);

	internal async Task<ApprovedGitHttpsTarget> PrepareAsync(SourceControlContext context, SourceControlExecutionTarget target,
		Func<string, CancellationToken, Task<IPAddress[]>> resolve, CancellationToken cancellationToken)
	{
		if (!options.Value.Enabled)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.Disabled);
		}
		await DemandReadTargetAsync(context, target, cancellationToken);
		var access = await store.FindAsync(context.TenantId, target.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		var configuration = policies.Resolve(context, access.Binding, target.Profile);
		var approved = await GitHttpsNetworkBoundary.ResolveAsync(context, access.Binding, target.Profile,
			configuration.Network, resolve, cancellationToken);
		approved = new(approved.Remote, approved.Profile, approved.CopyAddresses()) { AdministrativePolicy = configuration };
		policies.DemandCurrent(context, access.Binding, approved);
		await DemandReadTargetAsync(context, target, cancellationToken);
		return approved;
	}

	internal async Task FetchRevisionAsync(GitWorkspace workspace, SourceControlContext context, SourceControlExecutionTarget target,
		GitCommitId revision, CancellationToken cancellationToken)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.WriteTimeout);
		try
		{
			var approved = await PrepareAsync(context, target, deadline.Token);
			using var lease = await credentials.IssueAsync(context, target, approved, deadline.Token);
			await DemandReadTargetAsync(context, target, deadline.Token);
			var access = await store.FindAsync(context.TenantId, target.RepositoryId, deadline.Token)
				?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
			policies.DemandCurrent(context, access.Binding, approved);
			await git.FetchApprovedProfileRevisionAsync(workspace, context, access.Binding, approved, revision, lease, deadline.Token);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
		}
	}

	private async Task DemandReadTargetAsync(SourceControlContext context, SourceControlExecutionTarget target,
		CancellationToken cancellationToken)
	{
		var roles = await actors.ResolveAsync(context, cancellationToken);
		await store.DemandConnectionTargetAsync(context, target, roles, SourceControlOperationKind.Read, cancellationToken);
	}
}
