using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class SourceControlHttpsReadExecutor(PersistentSourceControlStore store, SourceControlWorkspace workspaces,
	ProfiledGitHttpsTransport transport, ISourceControlActorResolver actors, GitHttpsPolicyRegistry policies)
{
	internal bool HasCreatedWorkspace { get; private set; }

	internal Task<T> ExecuteAsync<T>(SourceControlContext context, Guid id, string worker, long fence,
		Func<GitWorkspace, AcceptedGitRevisionReadWork, CancellationToken, Task<T>> read, CancellationToken cancellationToken) =>
		ExecuteControlledAsync(context, id, worker, fence,
			(workspace, token) => transport.FetchClaimedRevisionReadAsync(workspace, context, id, worker, fence, token), read, cancellationToken);

	// The controlled entry exercises workspace and authority boundaries without replacing production transport policy.
	internal async Task<T> ExecuteControlledAsync<T>(SourceControlContext context, Guid id, string worker, long fence,
		Func<GitWorkspace, CancellationToken, Task> fetch,
		Func<GitWorkspace, AcceptedGitRevisionReadWork, CancellationToken, Task<T>> read, CancellationToken cancellationToken)
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

	private async Task<AcceptedGitRevisionReadWork> CurrentWorkAsync(SourceControlContext context, Guid id, string worker,
		long fence, CancellationToken cancellationToken) => await store.ReadClaimedHttpsRevisionReadAsync(context, id, worker, fence,
		await actors.ResolveAsync(context, cancellationToken), policies, cancellationToken);
}
