using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Project the original typed SSH request; results become visible only after a fenced durable finish.</summary>
internal sealed class ProfiledSshReadService(PersistentSourceControlStore store, ISourceControlActorResolver actors,
	GitSshPolicyRegistry policies, SourceControlClaimedSshReadRunner runner, ControlledGitProcess git, ProfiledGitSshTransport transport)
{
	internal async Task<T> ReadAsync<T>(SourceControlContext context, RepositoryBinding supplied, GitCommitId? revision,
		AcceptedGitReadQuery query, CancellationToken cancellationToken)
	{
		if (context.TenantId != supplied.TenantId)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
		var roles = await actors.ResolveAsync(context, cancellationToken);
		var target = await store.CaptureConnectionTargetAsync(context, supplied.Id, roles, SourceControlOperationKind.Read, cancellationToken);
		var id = await store.EnqueueSshReadAsync(context, target, roles, new(Guid.NewGuid().ToString("N")),
			new(revision) { Query = query }, policies, cancellationToken);
		var worker = "ssh-read-" + Guid.NewGuid().ToString("N");
		var fence = await store.TryClaimSshReadAsync(context, id, worker, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2),
			await actors.ResolveAsync(context, cancellationToken), policies, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.QuotaExceeded);
		var outcome = await runner.RunAsync<T>(context, id, worker, fence,
			(workspace, work, token) => ProjectAsync<T>(workspace, context, id, worker, fence, work, token), cancellationToken);
		if (outcome.State != SourceControlOperationState.Succeeded)
		{
			throw new SourceControlSecurityException(outcome.Error ?? SourceControlErrorCode.ResultUnknown);
		}
		return await store.ReadSshResultAsync<T>(context, id, await actors.ResolveAsync(context, cancellationToken), policies, cancellationToken);
	}

	internal async Task<SourceControlOperationState> RunClaimedAsync(SourceControlContext context, Guid id, string worker,
		long fence, CancellationToken cancellationToken)
	{
		var work = await store.ReadClaimedSshReadAsync(context, id, worker, fence,
			await actors.ResolveAsync(context, cancellationToken), policies, cancellationToken);
		return work.Request.Query?.Kind switch
		{
			GitReadQueryKind.Branches => await RunTypedAsync<SourceControlPage<string>>(context, id, worker, fence, cancellationToken),
			GitReadQueryKind.ResolveBranch => await RunTypedAsync<RevisionSelection>(context, id, worker, fence, cancellationToken),
			GitReadQueryKind.File => await RunTypedAsync<ModelSnapshot>(context, id, worker, fence, cancellationToken),
			GitReadQueryKind.ListFiles => await RunTypedAsync<SourceControlPage<RepositoryFile>>(context, id, worker, fence, cancellationToken),
			GitReadQueryKind.History => await RunTypedAsync<SourceControlPage<CommitSummary>>(context, id, worker, fence, cancellationToken),
			GitReadQueryKind.Diff => await RunTypedAsync<ModelDiff>(context, id, worker, fence, cancellationToken),
			_ => throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable)
		};
	}

	private async Task<SourceControlOperationState> RunTypedAsync<T>(SourceControlContext context, Guid id, string worker,
		long fence, CancellationToken cancellationToken) =>
		(await runner.RunWorkerAsync<T>(context, id, worker, fence,
			(workspace, work, token) => ProjectAsync<T>(workspace, context, id, worker, fence, work, token), cancellationToken)).State;

	private async Task<T> ProjectAsync<T>(GitWorkspace workspace, SourceControlContext context, Guid id, string worker,
		long fence, AcceptedGitSshReadWork work, CancellationToken cancellationToken)
	{
		var query = work.Request.Query ?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		if (query.IsRemoteQuery)
		{
			return await transport.ReadClaimedRemoteQueryAsync<T>(workspace, context, id, worker, fence, cancellationToken);
		}
		var revision = work.Request.Revision ?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		var binding = (await store.FindAsync(context.TenantId, work.Execution.Target.RepositoryId, cancellationToken))?.Binding
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		object result = query.Kind switch
		{
			GitReadQueryKind.ListFiles => await git.ListModelsAsync(workspace, binding,
				new(revision, query.Path!, query.PageSize, query.Cursor), cancellationToken),
			GitReadQueryKind.File => await git.ReadModelAsync(workspace, binding,
				new(revision, query.Path!), query.Generation, cancellationToken),
			GitReadQueryKind.History => await git.ReadModelHistoryAsync(workspace, binding,
				new(query.Path!, revision, query.PageSize, query.Cursor), cancellationToken),
			GitReadQueryKind.Diff => await git.CompareModelAsync(workspace, binding,
				new(revision, query.Snapshot!.Restore()), cancellationToken),
			_ => throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe)
		};
		return result is T typed ? typed : throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
	}
}
