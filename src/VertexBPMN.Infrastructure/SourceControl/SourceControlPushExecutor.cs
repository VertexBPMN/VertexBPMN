using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Finite push executor. The trusted host supplies fresh actor/tenant roles, never invented worker grants.</summary>
internal sealed class SourceControlPushExecutor(PersistentSourceControlStore store,
	SourceControlWorkspace workspaces, ControlledGitProcess git, GitHubAppTokenBroker tokens)
{
	internal async Task<PushReceipt> ExecuteAsync(SourceControlContext context, Guid id, string worker, long fence,
		Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveCurrentRoles, CancellationToken cancellationToken)
	{
		var roles = (await resolveCurrentRoles(cancellationToken)).ToArray();
		var work = await store.ReadPushWorkAsync(context, id, worker, fence, roles, cancellationToken);
		var workspace = await workspaces.CreateAsync(context, id, worker, fence, cancellationToken);
		await git.InitializeAsync(workspace, work.LocalCommit.Command.BaseCommit, cancellationToken);
		using var lease = await tokens.IssueAsync(context, work.Binding.Id, roles, RepositoryPermission.Push, cancellationToken);
		var saved = await store.ReadSavedResultAsync(context.TenantId, id, worker, fence, DateTimeOffset.UtcNow, cancellationToken);
		if (saved is null && work.State != SourceControlOperationState.Reconciling)
		{
			await git.FetchRevisionAsync(workspace, work.Binding.Remote, work.LocalCommit.Command.BaseCommit, lease, cancellationToken);
		}
		return await ExecutePreparedAsync(context, id, worker, fence, resolveCurrentRoles, workspace,
			(command, token) => git.ReadPushHeadAsync(workspace, work.Binding, command, lease, token),
			(command, token) => git.PushAsync(workspace, work.Binding, command, lease, token), cancellationToken);
	}

	// Isolated acceptance may supply a real local HTTPS transport and a prepared job-owned repository.
	internal async Task<PushReceipt> ExecutePreparedAsync(SourceControlContext context, Guid id, string worker, long fence,
		Func<CancellationToken, Task<IReadOnlyCollection<string>>> resolveCurrentRoles, GitWorkspace prepared,
		Func<PushCommand, CancellationToken, Task<GitCommitId?>> readHead,
		Func<PushCommand, CancellationToken, Task> push, CancellationToken cancellationToken)
	{
		var roles = (await resolveCurrentRoles(cancellationToken)).ToArray();
		var work = await store.ReadPushWorkAsync(context, id, worker, fence, roles, cancellationToken);
		var workspace = await workspaces.OpenOwnedAsync(context, id, worker, fence, prepared.Fence, cancellationToken);
		if (workspace != prepared)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}
		var receipt = new PushReceipt(id, work.Command.Commit, work.Command.WorkBranch);
		var saved = await store.ReadSavedResultAsync(context.TenantId, id, worker, fence, DateTimeOffset.UtcNow, cancellationToken);
		if (saved is not null)
		{
			ValidateIntent(saved, work, receipt);
			roles = (await resolveCurrentRoles(cancellationToken)).ToArray();
			work = await store.ReadPushWorkAsync(context, id, worker, fence, roles, cancellationToken);
			await store.PublishRemotePushAsync(context, work, worker, fence, async token =>
			{
				var head = await readHead(work.Command, token);
				if (GitPushReconciliation.AfterUncertainWrite(receipt.Commit, head) != GitPushReconciliationResult.Confirmed)
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
				}
			}, cancellationToken);
			return receipt; // Reconciliation never calls push, even if the remote reverted to the expected head.
		}
		if (work.State == SourceControlOperationState.Reconciling)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
		}
		var reconstructed = await git.BuildCommitAsync(workspace, work.Binding, work.LocalCommit.Command,
			work.LocalCommit.AcceptedAt, cancellationToken);
		if (reconstructed != work.Command.Commit)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		var before = await readHead(work.Command, cancellationToken);
		if (GitPushReconciliation.BeforeFirstWrite(work.Command, before) != GitPushReconciliationResult.ReadyToPush)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
		if (!await store.SaveResultAsync(context.TenantId, id, worker, fence,
			JsonSerializer.SerializeToUtf8Bytes(new StoredRemotePush(1, work.Binding.Id, receipt)), DateTimeOffset.UtcNow, cancellationToken))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
		}
		roles = (await resolveCurrentRoles(cancellationToken)).ToArray();
		work = await store.ReadPushWorkAsync(context, id, worker, fence, roles, cancellationToken);
		await store.PublishRemotePushAsync(context, work, worker, fence, async token =>
		{
			try
			{
				await push(work.Command, token);
			}
			catch (SourceControlSecurityException exception) when (exception.Code == SourceControlErrorCode.RevisionConflict)
			{
				throw new RejectedRemotePushException();
			}
			catch (SourceControlSecurityException exception) when (exception.Code == SourceControlErrorCode.ProviderUnavailable)
			{
				// A transport failure may follow a committed server-side write. Only inspect; never resend.
				var head = await readHead(work.Command, token);
				if (GitPushReconciliation.AfterUncertainWrite(receipt.Commit, head) != GitPushReconciliationResult.Confirmed)
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
				}
			}
		}, cancellationToken);
		return receipt;
	}

	private static void ValidateIntent(byte[] bytes, AcceptedPushWork work, PushReceipt expected)
	{
		try
		{
			var saved = JsonSerializer.Deserialize<StoredRemotePush>(bytes);
			if (saved is null || saved.SchemaVersion != 1 || saved.RepositoryId != work.Binding.Id || saved.Receipt != expected)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
		}
		catch (JsonException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
	}
}
