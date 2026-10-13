using System.Net;
using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ProfiledGitHttpsTransport
{
	internal async Task<PreparedGitRevisionRead> PrepareClaimedRevisionReadAsync(SourceControlContext context, Guid operationId,
		string worker, long fence, Func<string, CancellationToken, Task<IPAddress[]>> resolve, CancellationToken cancellationToken)
	{
		var work = await ReadCurrentClaimedRevisionAsync(context, operationId, worker, fence, cancellationToken);
		var target = work.Execution.Target;
		var access = await store.FindAsync(context.TenantId, target.RepositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		policies.DemandCurrent(context, access.Binding, target.Profile, work.Execution.Policy);
		var approved = await GitHttpsNetworkBoundary.ResolveAsync(context, access.Binding, target.Profile,
			work.Execution.Policy.Network, resolve, cancellationToken);
		approved = new(approved.Remote, approved.Profile, approved.CopyAddresses()) { AdministrativePolicy = work.Execution.Policy };
		DemandSameRevisionWork(work, await ReadCurrentClaimedRevisionAsync(context, operationId, worker, fence, cancellationToken));
		return new(work, approved);
	}

	internal async Task FetchClaimedRevisionReadAsync(GitWorkspace workspace, SourceControlContext context, Guid operationId,
		string worker, long fence, CancellationToken cancellationToken)
	{
		if (workspace.OperationId != operationId || workspace.Fence != fence)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.ReadTimeout);
		try
		{
			if ((await ReadCurrentClaimedRevisionAsync(context, operationId, worker, fence, deadline.Token)).Query?.IsRemoteQuery == true)
			{
				return; // ls-remote owns its single approval/credential lifecycle; no fetch is needed.
			}
			var prepared = await PrepareClaimedRevisionReadAsync(context, operationId, worker, fence, Dns.GetHostAddressesAsync, deadline.Token);
			var target = prepared.Work.Execution.Target;
			if (prepared.Work.Query?.IsRemoteQuery == true)
			{
				await git.InitializeAsync(workspace, deadline.Token);
			}
			else
			{
				await git.InitializeAsync(workspace, prepared.Work.FixedRevision, deadline.Token);
			}
			DemandSameRevisionWork(prepared.Work, await ReadCurrentClaimedRevisionAsync(context, operationId, worker, fence, deadline.Token));
			using var lease = await credentials.IssueAsync(context, target, prepared.Approved, deadline.Token);
			DemandSameRevisionWork(prepared.Work, await ReadCurrentClaimedRevisionAsync(context, operationId, worker, fence, deadline.Token));
			var access = await store.FindAsync(context.TenantId, target.RepositoryId, deadline.Token)
				?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
			policies.DemandCurrent(context, access.Binding, prepared.Approved);
			if (prepared.Work.Query?.Kind == GitReadQueryKind.History)
			{
				await git.FetchApprovedProfileHistoryAsync(workspace, context, access.Binding, prepared.Approved,
					prepared.Work.FixedRevision, lease, deadline.Token);
			}
			else if (prepared.Work.Query?.IsRemoteQuery != true)
			{
				await git.FetchApprovedProfileRevisionAsync(workspace, context, access.Binding, prepared.Approved,
					prepared.Work.FixedRevision, lease, deadline.Token);
			}
			DemandSameRevisionWork(prepared.Work, await ReadCurrentClaimedRevisionAsync(context, operationId, worker, fence, deadline.Token));
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
		}
	}

	private async Task<AcceptedGitRevisionReadWork> ReadCurrentClaimedRevisionAsync(SourceControlContext context, Guid operationId,
		string worker, long fence, CancellationToken cancellationToken)
	{
		var roles = await actors.ResolveAsync(context, cancellationToken);
		return await store.ReadClaimedHttpsRevisionReadAsync(context, operationId, worker, fence, roles, policies, cancellationToken);
	}

	private static void DemandSameRevisionWork(AcceptedGitRevisionReadWork expected, AcceptedGitRevisionReadWork current)
	{
		if (expected.Revision != current.Revision || expected.Execution.Target != current.Execution.Target
			|| JsonSerializer.Serialize(expected.Query) != JsonSerializer.Serialize(current.Query)
			|| !expected.Execution.Policy.Matches(current.Execution.Policy))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
	}
}
