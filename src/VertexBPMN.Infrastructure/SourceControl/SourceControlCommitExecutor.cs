using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>
/// Finite claimed-job executor. Roles must be freshly resolved by the trusted host;
/// no background scheduler may invent roles or copy them from browser input.
/// </summary>
internal sealed class SourceControlCommitExecutor(PersistentSourceControlStore store,
    SourceControlWorkspace workspaces, ControlledGitProcess git, GitHubAppTokenBroker tokens)
{
    internal async Task<CommitReceipt> ExecuteAsync(SourceControlContext context, Guid id, string worker,
        long fence, IReadOnlyCollection<string> currentRoles, CancellationToken cancellationToken)
    {
        var roles = currentRoles.ToArray();
        var work = await store.ReadCommitWorkAsync(context, id, worker, fence, roles, cancellationToken);
        var saved = await ReadIntentAsync(context, work, worker, fence, cancellationToken);
        GitWorkspace workspace;
        if (saved is not null)
            workspace = await workspaces.OpenOwnedAsync(context, id, worker, fence, saved.WorkspaceFence, cancellationToken);
        else
        {
            if (work.State == SourceControlOperationState.Reconciling)
                throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
            workspace = await workspaces.CreateAsync(context, id, worker, fence, cancellationToken);
            await git.InitializeAsync(workspace, cancellationToken);
            using var token = await tokens.IssueAsync(context, work.Binding.Id, roles, RepositoryPermission.Commit, cancellationToken);
            // No moving-head substitution: BuildCommit uses the accepted full base OID or fails.
            await git.FetchRevisionAsync(workspace, work.Binding.Remote, work.Command.BaseCommit, token, cancellationToken);
        }
        return await ExecutePreparedAsync(context, id, worker, fence, roles, workspace, cancellationToken);
    }

    // A prepared private workspace also permits local native acceptance without a live GitHub installation.
    internal async Task<CommitReceipt> ExecutePreparedAsync(SourceControlContext context, Guid id, string worker,
        long fence, IReadOnlyCollection<string> currentRoles, GitWorkspace prepared, CancellationToken cancellationToken)
    {
        var roles = currentRoles.ToArray();
        var work = await store.ReadCommitWorkAsync(context, id, worker, fence, roles, cancellationToken);
        var saved = await ReadIntentAsync(context, work, worker, fence, cancellationToken);
        if (saved is null && work.State == SourceControlOperationState.Reconciling)
            throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
        var expectedFence = saved?.WorkspaceFence ?? fence;
        var workspace = await workspaces.OpenOwnedAsync(context, id, worker, fence, expectedFence, cancellationToken);
        if (prepared != workspace) throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        // Rebuild immutable objects only, never replay a published ref. This verifies the receipt
        // against its exact parent, time, message and every byte before trusting it on recovery.
        var commit = await git.BuildCommitAsync(workspace, work.Binding, work.Command, work.AcceptedAt, cancellationToken);
        var receipt = new CommitReceipt(id, work.Command.SessionId, commit, work.Command.WorkBranch,
            work.Command.Snapshots.Select(x => new CommittedSnapshot(x.Path, x.DocumentGeneration, x.LocalRevision, x.ContentSha256)).ToArray());
        if (saved is not null)
        {
            if (!JsonSerializer.SerializeToUtf8Bytes(saved.Receipt).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(receipt)))
                throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
        }
        else
        {
            var intent = new StoredLocalCommit(1, work.Binding.Id, workspace.Fence, receipt);
            if (!await store.SaveResultAsync(context.TenantId, id, worker, fence, JsonSerializer.SerializeToUtf8Bytes(intent),
                DateTimeOffset.UtcNow, cancellationToken))
                throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
        }
        // Authorization/lease must still be current after Git object construction.
        work = await store.ReadCommitWorkAsync(context, id, worker, fence, roles, cancellationToken);
        await store.PublishLocalCommitAsync(context, work, worker, fence,
            token => git.PublishLocalCommitAsync(workspace, receipt.WorkBranch, receipt.Commit, token), cancellationToken);
        return receipt;
    }

    private async Task<StoredLocalCommit?> ReadIntentAsync(SourceControlContext context, AcceptedCommitWork work,
        string worker, long fence, CancellationToken cancellationToken)
    {
        var bytes = await store.ReadSavedResultAsync(context.TenantId, work.Command.OperationId, worker, fence,
            DateTimeOffset.UtcNow, cancellationToken);
        if (bytes is null) return null;
        try
        {
            var saved = JsonSerializer.Deserialize<StoredLocalCommit>(bytes);
            if (saved is null || saved.SchemaVersion != 1 || saved.RepositoryId != work.Binding.Id
                || saved.WorkspaceFence <= 0 || saved.WorkspaceFence > fence || saved.Receipt is null
                || saved.Receipt.OperationId != work.Command.OperationId || saved.Receipt.SessionId != work.Command.SessionId
                || saved.Receipt.WorkBranch != work.Command.WorkBranch || saved.Receipt.Commit is null)
                throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
            return saved;
        }
        catch (SourceControlSecurityException) { throw; }
        catch { throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
    }
}
