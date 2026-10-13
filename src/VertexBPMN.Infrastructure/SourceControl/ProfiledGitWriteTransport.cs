using System.Net;
using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Current actor, accepted write context and one pinned transport approval for each bounded native operation.</summary>
internal sealed partial class ProfiledGitWriteTransport(PersistentSourceControlStore store, ISourceControlActorResolver actors,
	ProfiledGitCredentialBroker credentials, ControlledGitProcess git, ProfiledSshCredentialBroker? sshCredentials = null)
{
	internal async Task FetchBaseAsync(GitWorkspace workspace, SourceControlContext context, Guid id, string worker,
		long fence, GitCommitId revision, CancellationToken cancellationToken)
	{
		if (await IsSshAsync(context, id, worker, fence, cancellationToken))
		{
			await FetchSshBaseAsync(workspace, context, id, worker, fence, revision, cancellationToken);
			return;
		}
		_ = await WithCurrentAsync(workspace, context, id, worker, fence, async (binding, approved, lease, token) =>
		{
			var roles = await actors.ResolveAsync(context, token);
			var accepted = await AcceptedBaseAsync(context, id, worker, fence, roles, token);
			if (accepted != revision)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			await git.FetchApprovedProfileRevisionAsync(workspace, context, binding, approved, revision, lease, token);
			return true;
		}, ProfiledGitRemoteOperation.Fetch, cancellationToken);
	}

	internal async Task<GitCommitId?> ReadHeadAsync(GitWorkspace workspace, SourceControlContext context, Guid id, string worker,
		long fence, PushCommand command, CancellationToken cancellationToken)
	{
		if (await IsSshAsync(context, id, worker, fence, cancellationToken))
		{
			return await ReadSshHeadAsync(workspace, context, id, worker, fence, command, cancellationToken);
		}
		return await WithCurrentAsync(workspace, context, id, worker, fence,
			async (binding, approved, lease, token) =>
			{
				await DemandAcceptedPushAsync(context, id, worker, fence, command, token);
				return await git.ReadApprovedProfilePushHeadAsync(workspace, context, binding, approved, command, lease, token);
			}, ProfiledGitRemoteOperation.InspectRef, cancellationToken);
	}

	internal async Task PushAsync(GitWorkspace workspace, SourceControlContext context, Guid id, string worker, long fence,
		PushCommand command, CancellationToken cancellationToken)
	{
		if (await IsSshAsync(context, id, worker, fence, cancellationToken))
		{
			await PushSshAsync(workspace, context, id, worker, fence, command, cancellationToken);
			return;
		}
		_ = await WithCurrentAsync(workspace, context, id, worker, fence, async (binding, approved, lease, token) =>
		{
			await DemandAcceptedPushAsync(context, id, worker, fence, command, token);
			await git.PushApprovedProfileAsync(workspace, context, binding, approved, command, lease, token);
			return true;
		}, ProfiledGitRemoteOperation.PublishRef, cancellationToken);
	}

	private async Task DemandAcceptedPushAsync(SourceControlContext context, Guid id, string worker, long fence,
		PushCommand command, CancellationToken cancellationToken)
	{
		var roles = await actors.ResolveAsync(context, cancellationToken);
		var accepted = await store.ReadPushWorkAsync(context, id, worker, fence, roles, cancellationToken);
		if (accepted.Command != command)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}

	private async Task<GitCommitId> AcceptedBaseAsync(SourceControlContext context, Guid id, string worker, long fence,
		IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		var execution = await store.ReadClaimedWriteExecutionAsync(context, id, worker, fence, roles, cancellationToken);
		return execution.Target.RequiredCapabilities == SourceControlCapability.Commit
			? (await store.ReadCommitWorkAsync(context, id, worker, fence, roles, cancellationToken)).Command.BaseCommit
			: (await store.ReadPushWorkAsync(context, id, worker, fence, roles, cancellationToken)).LocalCommit.Command.BaseCommit;
	}

	private async Task<T> WithCurrentAsync<T>(GitWorkspace workspace, SourceControlContext context, Guid id, string worker, long fence,
		Func<RepositoryBinding, ApprovedGitHttpsTarget, IGitCredentialLease, CancellationToken, Task<T>> execute,
		ProfiledGitRemoteOperation operation, CancellationToken cancellationToken)
	{
		if (workspace.OperationId != id || workspace.Fence != fence)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
		var execution = await CurrentAsync(context, id, worker, fence, cancellationToken);
		var binding = (await store.FindAsync(context.TenantId, execution.Target.RepositoryId, cancellationToken))?.Binding
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		var approval = await GitHttpsNetworkBoundary.ResolveAsync(context, binding, execution.Target.Profile,
			execution.Https!.Policy.Network, Dns.GetHostAddressesAsync, cancellationToken);
		approval = new(approval.Remote, approval.Profile, approval.CopyAddresses())
		{
			AdministrativePolicy = execution.Https.Policy,
			RequiredCapabilities = execution.Target.RequiredCapabilities
		};
		DemandSameExecution(execution, await CurrentAsync(context, id, worker, fence, cancellationToken));
		using var lease = await credentials.IssueAsync(context, execution.Target, approval, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		DemandSameExecution(execution, await CurrentAsync(context, id, worker, fence, cancellationToken));
		var result = await execute(binding, approval, lease, cancellationToken);
		try
		{
			DemandSameExecution(execution, await CurrentAsync(context, id, worker, fence, cancellationToken));
		}
		catch (SourceControlSecurityException) when (operation == ProfiledGitRemoteOperation.PublishRef)
		{
			// A native push might already have written. Preserve its intent for inspection, never report a pre-write conflict.
			throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
		}
		return result;
	}

	private async Task<AcceptedGitWriteExecution> CurrentAsync(SourceControlContext context, Guid id, string worker, long fence,
		CancellationToken cancellationToken) => await store.ReadClaimedWriteExecutionAsync(context, id, worker, fence,
		await actors.ResolveAsync(context, cancellationToken), cancellationToken);

	private static void DemandSameExecution(AcceptedGitWriteExecution expected, AcceptedGitWriteExecution current)
	{
		if (JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(current))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
	}
}
