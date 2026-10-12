using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ProfiledGitReadService
{
	internal async Task<SourceControlOperationState> RunClaimedAsync(SourceControlContext context, Guid id, string worker,
		long fence, CancellationToken cancellationToken)
	{
		var work = await store.ReadClaimedHttpsRevisionReadAsync(context, id, worker, fence,
			await actors.ResolveAsync(context, cancellationToken), policies, cancellationToken);
		return work.Query?.Kind switch
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
}
