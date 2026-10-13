using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class SourceControlSshReadExecutor(PersistentSourceControlStore store, SourceControlWorkspace workspaces,
	ProfiledGitSshTransport transport, ISourceControlActorResolver actors, GitSshPolicyRegistry policies)
{
	internal bool HasCreatedWorkspace { get; private set; }

	internal Task<T> ExecuteAsync<T>(SourceControlContext context, Guid id, string worker, long fence,
		Func<GitWorkspace, AcceptedGitSshReadWork, CancellationToken, Task<T>> read, CancellationToken cancellationToken) =>
		ExecuteControlledAsync(context, id, worker, fence,
			async (workspace, token) =>
			{
				if ((await CurrentWorkAsync(context, id, worker, fence, token)).Request.Query?.IsRemoteQuery != true)
				{
					await transport.FetchClaimedReadAsync(workspace, context, id, worker, fence, token);
				}
			}, read, cancellationToken);

	// The controlled entry exercises workspace and authority boundaries without replacing production transport policy.
	internal async Task<T> ExecuteControlledAsync<T>(SourceControlContext context, Guid id, string worker, long fence,
		Func<GitWorkspace, CancellationToken, Task> fetch,
		Func<GitWorkspace, AcceptedGitSshReadWork, CancellationToken, Task<T>> read, CancellationToken cancellationToken)
	{
		var work = await CurrentWorkAsync(context, id, worker, fence, cancellationToken);
		var workspace = await workspaces.CreateAsync(context, id, worker, fence, cancellationToken);
		HasCreatedWorkspace = true;
		await fetch(workspace, cancellationToken);
		await CurrentWorkAsync(context, id, worker, fence, cancellationToken);
		var result = await read(workspace, work, cancellationToken);
		await CurrentWorkAsync(context, id, worker, fence, cancellationToken);
		return result;
	}

	private async Task<AcceptedGitSshReadWork> CurrentWorkAsync(SourceControlContext context, Guid id, string worker,
		long fence, CancellationToken cancellationToken) => await store.ReadClaimedSshReadAsync(context, id, worker, fence,
		await actors.ResolveAsync(context, cancellationToken), policies, cancellationToken);
}
