using System.Net;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ProfiledGitSshTransport
{
	internal async Task FetchClaimedReadAsync(GitWorkspace workspace, SourceControlContext context, Guid id, string worker,
		long fence, CancellationToken cancellationToken)
	{
		DemandWorkspace(workspace, id, fence);
		var prepared = await PrepareClaimedReadAsync(context, id, worker, fence, Dns.GetHostAddressesAsync, cancellationToken);
		if (prepared.Work.Request.Query?.IsRemoteQuery == true)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		var revision = prepared.Work.Request.Revision ?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		await git.InitializeAsync(workspace, revision, cancellationToken);
		await DemandCurrentAsync(cancellationToken);
		using var lease = await credentials.IssueAsync(context, prepared.Approved, cancellationToken);
		if (prepared.Work.Request.Query?.Kind == GitReadQueryKind.History)
		{
			await git.FetchApprovedSshHistoryAsync(workspace, prepared.Approved, revision, lease, DemandCurrentAsync, cancellationToken);
		}
		else
		{
			await git.FetchApprovedSshRevisionAsync(workspace, prepared.Approved, revision, lease, DemandCurrentAsync, cancellationToken);
		}
		async Task DemandCurrentAsync(CancellationToken token) =>
			DemandSameWork(prepared.Work, await ReadCurrentAsync(context, id, worker, fence, token));
	}

	internal async Task<T> ReadClaimedRemoteQueryAsync<T>(GitWorkspace workspace, SourceControlContext context, Guid id,
		string worker, long fence, CancellationToken cancellationToken)
	{
		DemandWorkspace(workspace, id, fence);
		var prepared = await PrepareClaimedReadAsync(context, id, worker, fence, Dns.GetHostAddressesAsync, cancellationToken);
		var query = prepared.Work.Request.Query;
		if (query?.IsRemoteQuery != true)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		var binding = (await store.FindAsync(context.TenantId, prepared.Work.Execution.Target.RepositoryId, cancellationToken))?.Binding
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		await git.InitializeAsync(workspace, cancellationToken);
		await DemandCurrentAsync(cancellationToken);
		using var lease = await credentials.IssueAsync(context, prepared.Approved, cancellationToken);
		object result = query.Kind switch
		{
			GitReadQueryKind.Branches => await git.ListApprovedSshBranchesAsync(workspace, context, binding, prepared.Approved,
				lease, query.PageSize, query.Cursor, DemandCurrentAsync, cancellationToken),
			GitReadQueryKind.ResolveBranch => await git.ResolveApprovedSshBranchAsync(workspace, context, binding, prepared.Approved,
				lease, query.Path!, DemandCurrentAsync, cancellationToken),
			_ => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput)
		};
		return result is T typed ? typed : throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		async Task DemandCurrentAsync(CancellationToken token) =>
			DemandSameWork(prepared.Work, await ReadCurrentAsync(context, id, worker, fence, token));
	}

	private static void DemandWorkspace(GitWorkspace workspace, Guid id, long fence)
	{
		if (workspace.OperationId != id || workspace.Fence != fence)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
	}
}
