using System.Text.Json;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Typed accepted reads and durable results; the public provider resolves a server-owned binding before routing here.</summary>
internal sealed partial class ProfiledGitReadService(PersistentSourceControlStore store, ISourceControlActorResolver actors,
	GitHttpsPolicyRegistry policies, SourceControlClaimedReadRunner runner, ControlledGitProcess git,
	ProfiledGitHttpsTransport transport, IOptions<SourceControlOptions> options, ProfiledSshReadService? sshReads = null)
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
		if (target.Profile.Transport == GitTransportKind.Ssh)
		{
			return await (sshReads ?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable))
				.ReadAsync<T>(context, supplied, revision, query, cancellationToken);
		}
		var binding = (await store.FindAsync(context.TenantId, supplied.Id, cancellationToken))?.Binding
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		query.Validate(binding, revision, options.Value.Limits);
		var id = await store.EnqueueProfiledReadAsync(context, target, roles, new(Guid.NewGuid().ToString("N")),
			JsonSerializer.SerializeToUtf8Bytes(new AcceptedGitRevisionRead(revision) { Query = query }), policies, cancellationToken);
		var worker = "profiled-read-" + Guid.NewGuid().ToString("N");
		var fence = await store.TryClaimHttpsRevisionReadAsync(context, id, worker, DateTimeOffset.UtcNow,
			TimeSpan.FromMinutes(2), await actors.ResolveAsync(context, cancellationToken), policies, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.QuotaExceeded);
		var outcome = await runner.RunAsync<T>(context, id, worker, fence,
			(workspace, work, token) => ProjectAsync<T>(workspace, context, id, worker, fence, work, token), cancellationToken);
		if (outcome.State != SourceControlOperationState.Succeeded)
		{
			throw new SourceControlSecurityException(outcome.Error ?? SourceControlErrorCode.ResultUnknown);
		}
		return await store.ReadHttpsResultAsync<T>(context, id, await actors.ResolveAsync(context, cancellationToken), policies, cancellationToken);
	}

	private async Task<T> ProjectAsync<T>(GitWorkspace workspace, SourceControlContext context, Guid id, string worker,
		long fence, AcceptedGitRevisionReadWork work, CancellationToken cancellationToken)
	{
		var query = work.Query ?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		if (query.IsRemoteQuery)
		{
			return await transport.ReadClaimedRemoteQueryAsync<T>(workspace, context, id, worker, fence, cancellationToken);
		}
		var binding = (await store.FindAsync(context.TenantId, work.Execution.Target.RepositoryId, cancellationToken))?.Binding
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		object result = query.Kind switch
		{
			GitReadQueryKind.ListFiles => await git.ListModelsAsync(workspace, binding,
				new(work.FixedRevision, query.Path!, query.PageSize, query.Cursor), cancellationToken),
			GitReadQueryKind.File => await git.ReadModelAsync(workspace, binding,
				new(work.FixedRevision, query.Path!), query.Generation, cancellationToken),
			GitReadQueryKind.History => await git.ReadModelHistoryAsync(workspace, binding,
				new(query.Path!, work.FixedRevision, query.PageSize, query.Cursor), cancellationToken),
			GitReadQueryKind.Diff => await git.CompareModelAsync(workspace, binding,
				new(work.FixedRevision, query.Snapshot!.Restore()), cancellationToken),
			_ => throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe)
		};
		return result is T typed ? typed : throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
	}
}
