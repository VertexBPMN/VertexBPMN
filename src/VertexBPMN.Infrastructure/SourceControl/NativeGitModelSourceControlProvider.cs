using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Private claimed workspaces and current external authority checks; never browser-supplied bindings.</summary>
internal sealed class NativeGitModelSourceControlProvider(PersistentSourceControlStore store,
	SourceControlWorkspace workspaces, ControlledGitProcess git, GitHubAppTokenBroker tokens,
	ISourceControlActorResolver actors, SourceControlClaimedCommitRunner commits,
	SourceControlClaimedPushRunner pushes, IOptions<SourceControlOptions> options) : IModelSourceControlProvider
{
	public Task<SourceControlAvailability> GetAvailabilityAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var reason = !options.Value.Enabled ? SourceControlErrorCode.Disabled
			: !File.Exists(options.Value.GitExecutablePath) ? SourceControlErrorCode.GitUnavailable
			: string.IsNullOrWhiteSpace(options.Value.IdentityCredentialReference) ? SourceControlErrorCode.CredentialUnavailable
			: (SourceControlErrorCode?)null;
		return Task.FromResult(new SourceControlAvailability(reason is null,
			reason is null ? SourceControlCapability.Read | SourceControlCapability.Commit | SourceControlCapability.Push : SourceControlCapability.None, reason));
	}

	public Task<SourceControlPage<string>> ListBranchesAsync(SourceControlContext context, RepositoryBinding binding,
		int pageSize, string? cursor, CancellationToken cancellationToken) => ReadAsync(context, binding, null,
		(workspace, current, lease, token) => git.ListRemoteBranchesAsync(workspace, current, lease, pageSize, cursor, token), cancellationToken);

	public Task<RevisionSelection> ResolveBranchAsync(SourceControlContext context, RepositoryBinding binding,
		string branch, CancellationToken cancellationToken) => ReadAsync(context, binding, null,
		(workspace, current, lease, token) => git.ResolveRemoteBranchAsync(workspace, context, current, branch, lease, token), cancellationToken);

	public Task<SourceControlPage<RepositoryFile>> ListFilesAsync(SourceControlContext context, RepositoryBinding binding,
		TreeRequest request, CancellationToken cancellationToken) => ReadAsync(context, binding, request.Commit,
		(workspace, current, _, token) => git.ListModelsAsync(workspace, current, request, token), cancellationToken);

	public Task<ModelSnapshot> ReadFileAsync(SourceControlContext context, RepositoryBinding binding,
		FileReadRequest request, Guid documentGeneration, CancellationToken cancellationToken) => ReadAsync(context, binding, request.Commit,
		(workspace, current, _, token) => git.ReadModelAsync(workspace, current, request, documentGeneration, token), cancellationToken);

	public Task<SourceControlPage<CommitSummary>> ReadHistoryAsync(SourceControlContext context, RepositoryBinding binding,
		HistoryRequest request, CancellationToken cancellationToken) => ReadAsync(context, binding, request.Commit,
		async (workspace, current, lease, token) =>
		{
			await git.FetchHistoryAsync(workspace, current.Remote, request.Commit, lease, token);
			return await git.ReadModelHistoryAsync(workspace, current, request, token);
		}, cancellationToken);

	public Task<ModelDiff> CompareAsync(SourceControlContext context, RepositoryBinding binding,
		DiffRequest request, CancellationToken cancellationToken) => ReadAsync(context, binding, request.BaseCommit,
		(workspace, current, _, token) => git.CompareModelAsync(workspace, current, request, token), cancellationToken);

	private async Task<T> ReadAsync<T>(SourceControlContext context, RepositoryBinding supplied, GitCommitId? revision,
		Func<GitWorkspace, RepositoryBinding, GitHubTokenLease, CancellationToken, Task<T>> execute, CancellationToken cancellationToken)
	{
		if (context.TenantId != supplied.TenantId) throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.ReadTimeout);
		var roles = await actors.ResolveAsync(context, deadline.Token);
		var access = await AuthorizeAsync(context, supplied.Id, roles, RepositoryPermission.Read, deadline.Token);
		var id = await store.EnqueueAsync(context, access.Binding.Id, roles, SourceControlOperationKind.Read,
			new(Guid.NewGuid().ToString("N")), new byte[] { 1 }, deadline.Token);
		var worker = "read-" + Guid.NewGuid().ToString("N");
		var fence = await store.TryClaimAsync(context.TenantId, id, worker, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), deadline.Token)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.QuotaExceeded);
		GitWorkspace? workspace = null;
		try
		{
			workspace = await workspaces.CreateAsync(context, id, worker, fence, deadline.Token);
			if (revision is null) await git.InitializeAsync(workspace, deadline.Token);
			else await git.InitializeAsync(workspace, revision, deadline.Token);
			using var lease = await tokens.IssueAsync(context, access.Binding.Id, roles, RepositoryPermission.Read, deadline.Token);
			if (revision is not null) await git.FetchRevisionAsync(workspace, access.Binding.Remote, revision, lease, deadline.Token);
			var result = await execute(workspace, access.Binding, lease, deadline.Token);
			var current = await AuthorizeAsync(context, supplied.Id, await actors.ResolveAsync(context, deadline.Token), RepositoryPermission.Read, deadline.Token);
			if (current.Revision != access.Revision) throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
			if (!await store.FinishAsync(context.TenantId, id, worker, fence, SourceControlOperationState.Succeeded, DateTimeOffset.UtcNow, deadline.Token))
				throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
			return result;
		}
		finally
		{
			using var finish = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			// Read effects are private only; maintenance handles retained directories after failed cleanup.
			await store.FinishAsync(context.TenantId, id, worker, fence, SourceControlOperationState.Failed, DateTimeOffset.UtcNow, finish.Token);
			if (workspace is not null) await workspaces.CleanupAsync(context, id, fence, finish.Token);
		}
	}

	private async Task<RepositoryAccessSnapshot> AuthorizeAsync(SourceControlContext context, Guid repositoryId,
		IReadOnlyCollection<string> roles, RepositoryPermission permission, CancellationToken cancellationToken)
	{
		var access = await store.FindAsync(context.TenantId, repositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		RepositoryAccessPolicy.Demand(context, access.Binding, roles, access.Grants, permission);
		return access;
	}

	public async Task<CommitReceipt> CommitAsync(SourceControlContext context, RepositoryBinding binding,
		CommitCommand command, CancellationToken cancellationToken)
	{
		var roles = await actors.ResolveAsync(context, cancellationToken);
		await AuthorizeAsync(context, binding.Id, roles, RepositoryPermission.Commit, cancellationToken);
		await DemandOwnedOperationAsync(context, binding, command.OperationId, SourceControlOperationKind.Commit, roles, cancellationToken);
		var worker = "commit-" + Guid.NewGuid().ToString("N");
		var fence = await store.TryClaimAsync(context.TenantId, command.OperationId, worker, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		var work = await store.ReadCommitWorkAsync(context, command.OperationId, worker, fence, roles, cancellationToken);
		if (work.Command.SessionId != command.SessionId || work.Command.BaseCommit != command.BaseCommit
			|| work.Command.WorkBranch != command.WorkBranch || work.Command.Message != command.Message
			|| !work.Command.Snapshots.Select(x => (x.Path, x.ContentSha256, x.DocumentGeneration, x.LocalRevision))
				.SequenceEqual(command.Snapshots.Select(x => (x.Path, x.ContentSha256, x.DocumentGeneration, x.LocalRevision))))
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		var result = await commits.RunAsync(context, command.OperationId, worker, fence, token => actors.ResolveAsync(context, token), cancellationToken);
		return result.Receipt ?? throw new SourceControlSecurityException(result.Error ?? SourceControlErrorCode.ResultUnknown);
	}

	public async Task<PushReceipt> PushAsync(SourceControlContext context, RepositoryBinding binding,
		PushCommand command, CancellationToken cancellationToken)
	{
		var roles = await actors.ResolveAsync(context, cancellationToken);
		await AuthorizeAsync(context, binding.Id, roles, RepositoryPermission.Push, cancellationToken);
		await DemandOwnedOperationAsync(context, binding, command.OperationId, SourceControlOperationKind.Push, roles, cancellationToken);
		var worker = "push-" + Guid.NewGuid().ToString("N");
		var fence = await store.TryClaimAsync(context.TenantId, command.OperationId, worker, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		var work = await store.ReadPushWorkAsync(context, command.OperationId, worker, fence, roles, cancellationToken);
		if (work.Command != command) throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		var result = await pushes.RunAsync(context, command.OperationId, worker, fence, token => actors.ResolveAsync(context, token), cancellationToken);
		return result.Receipt ?? throw new SourceControlSecurityException(result.Error ?? SourceControlErrorCode.ResultUnknown);
	}

	private async Task DemandOwnedOperationAsync(SourceControlContext context, RepositoryBinding binding, Guid id,
		SourceControlOperationKind kind, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
	{
		if (context.TenantId != binding.TenantId) throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		var operation = await store.GetOperationAsync(context, id, roles, cancellationToken);
		if (operation?.ActorId != context.ActorId || operation.RepositoryId != binding.Id || operation.Kind != kind)
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
	}
}
