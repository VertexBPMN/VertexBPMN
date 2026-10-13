using System.Net;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ProfiledGitHttpsTransport
{
	internal async Task<T> ReadClaimedRemoteQueryAsync<T>(GitWorkspace workspace, SourceControlContext context, Guid id,
		string worker, long fence, CancellationToken cancellationToken)
	{
		if (workspace.OperationId != id || workspace.Fence != fence)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
		var prepared = await PrepareClaimedRevisionReadAsync(context, id, worker, fence, Dns.GetHostAddressesAsync, cancellationToken);
		var query = prepared.Work.Query;
		if (query?.IsRemoteQuery != true)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		await git.InitializeAsync(workspace, cancellationToken);
		DemandSameRevisionWork(prepared.Work, await ReadCurrentClaimedRevisionAsync(context, id, worker, fence, cancellationToken));
		using var lease = await credentials.IssueAsync(context, prepared.Work.Execution.Target, prepared.Approved, cancellationToken);
		DemandSameRevisionWork(prepared.Work, await ReadCurrentClaimedRevisionAsync(context, id, worker, fence, cancellationToken));
		var binding = (await store.FindAsync(context.TenantId, prepared.Work.Execution.Target.RepositoryId, cancellationToken))?.Binding
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		policies.DemandCurrent(context, binding, prepared.Approved);
		object result = query.Kind switch
		{
			GitReadQueryKind.Branches => await git.ListApprovedProfileBranchesAsync(workspace, context, binding,
				prepared.Approved, lease, query.PageSize, query.Cursor, cancellationToken),
			GitReadQueryKind.ResolveBranch => await git.ResolveApprovedProfileBranchAsync(workspace, context, binding,
				prepared.Approved, lease, query.Path!, cancellationToken),
			_ => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput)
		};
		DemandSameRevisionWork(prepared.Work, await ReadCurrentClaimedRevisionAsync(context, id, worker, fence, cancellationToken));
		return result is T typed ? typed : throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
	}
}
