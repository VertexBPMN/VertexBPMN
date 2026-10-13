using System.Net;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ProfiledGitWriteTransport
{
	private async Task<bool> IsSshAsync(SourceControlContext context, Guid id, string worker, long fence, CancellationToken cancellationToken) =>
		(await CurrentAsync(context, id, worker, fence, cancellationToken)).Ssh is not null;

	private async Task FetchSshBaseAsync(GitWorkspace workspace, SourceControlContext context, Guid id, string worker,
		long fence, GitCommitId revision, CancellationToken cancellationToken)
	{
		await WithSshAsync(workspace, context, id, worker, fence, async (approved, lease, demandCurrent, token) =>
		{
			var roles = await actors.ResolveAsync(context, token);
			if (await AcceptedBaseAsync(context, id, worker, fence, roles, token) != revision)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			await git.FetchApprovedSshRevisionAsync(workspace, approved, revision, lease, demandCurrent, token);
			return true;
		}, ProfiledGitRemoteOperation.Fetch, cancellationToken);
	}

	private Task<GitCommitId?> ReadSshHeadAsync(GitWorkspace workspace, SourceControlContext context, Guid id, string worker,
		long fence, PushCommand command, CancellationToken cancellationToken) =>
		WithSshAsync(workspace, context, id, worker, fence, async (approved, lease, demandCurrent, token) =>
		{
			await DemandAcceptedPushAsync(context, id, worker, fence, command, token);
			return await git.ReadApprovedSshPushHeadAsync(workspace, approved, command, lease, demandCurrent, token);
		}, ProfiledGitRemoteOperation.InspectRef, cancellationToken);

	private async Task PushSshAsync(GitWorkspace workspace, SourceControlContext context, Guid id, string worker, long fence,
		PushCommand command, CancellationToken cancellationToken) =>
		_ = await WithSshAsync(workspace, context, id, worker, fence, async (approved, lease, demandCurrent, token) =>
		{
			await DemandAcceptedPushAsync(context, id, worker, fence, command, token);
			var binding = (await store.FindAsync(context.TenantId, approved.Target.RepositoryId, token))!.Binding;
			await git.PushApprovedSshAsync(workspace, binding, approved, command, lease, demandCurrent, token);
			return true;
		}, ProfiledGitRemoteOperation.PublishRef, cancellationToken);

	private async Task<T> WithSshAsync<T>(GitWorkspace workspace, SourceControlContext context, Guid id, string worker, long fence,
		Func<ApprovedGitSshTarget, GitSshCredentialLease, Func<CancellationToken, Task>, CancellationToken, Task<T>> execute,
		ProfiledGitRemoteOperation operation, CancellationToken cancellationToken)
	{
		if (workspace.OperationId != id || workspace.Fence != fence)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
		var execution = await CurrentAsync(context, id, worker, fence, cancellationToken);
		var ssh = execution.Ssh ?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		var binding = (await store.FindAsync(context.TenantId, execution.Target.RepositoryId, cancellationToken))?.Binding
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		var approved = await GitSshNetworkBoundary.ResolveAsync(context, binding, ssh.Target, ssh.Policy.Network,
			ssh.Policy.Trust, Dns.GetHostAddressesAsync, cancellationToken);
		async Task DemandCurrentAsync(CancellationToken token) =>
			DemandSameExecution(execution, await CurrentAsync(context, id, worker, fence, token));
		await DemandCurrentAsync(cancellationToken);
		using var lease = await (sshCredentials ?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable))
			.IssueAsync(context, approved, cancellationToken);
		await DemandCurrentAsync(cancellationToken);
		var result = await execute(approved, lease, DemandCurrentAsync, cancellationToken);
		try
		{
			await DemandCurrentAsync(cancellationToken);
		}
		catch (SourceControlSecurityException) when (operation == ProfiledGitRemoteOperation.PublishRef)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
		}
		return result;
	}
}
