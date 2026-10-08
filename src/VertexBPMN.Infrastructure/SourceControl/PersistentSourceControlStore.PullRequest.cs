using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
    public async Task<Guid> EnqueuePullRequestAsync(SourceControlContext context, Guid repositoryId,
        IReadOnlyCollection<string> roles, SourceControlIdempotencyKey key, PullRequestJobSubmission submission,
        CancellationToken cancellationToken)
    {
        Relational();
        ArgumentNullException.ThrowIfNull(submission);
        if (submission.PushOperationId == Guid.Empty || string.IsNullOrWhiteSpace(submission.Title)
            || submission.Title.Length > 256 || submission.Title.Any(char.IsControl)
            || submission.Description is null || submission.Description.Length > 16_384)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        }
        SourceControlInputPolicy.ValidateBranch(submission.BaseBranch);
        var copiedRoles = roles.ToArray();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var access = await FindAsync(context.TenantId, repositoryId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        RepositoryAccessPolicy.Demand(context, access.Binding, copiedRoles, access.Grants, RepositoryPermission.PullRequest);
        if (access.Binding.Remote.Host != "github.com"
            || submission.BaseBranch != access.Binding.DefaultBranch && submission.BaseBranch != access.Binding.ReleaseBranch)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        }
        var source = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == submission.PushOperationId && x.RepositoryId == repositoryId && x.TenantId == context.TenantId
            && x.ActorId == context.ActorId && x.Kind == (int)SourceControlOperationKind.Push
            && x.State == (int)SourceControlOperationState.Pushed, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        StoredRemotePush saved;
        try
        {
            saved = JsonSerializer.Deserialize<StoredRemotePush>(Unprotect(context, repositoryId, source.ProtectedResult ?? ""))!;
            if (saved is null || saved.SchemaVersion != 1 || saved.RepositoryId != repositoryId
                || saved.Receipt is null || saved.Receipt.OperationId != source.Id || saved.Receipt.Commit is null
                || !saved.Receipt.WorkBranch.StartsWith("vertex/", StringComparison.Ordinal)
                || saved.Receipt.WorkBranch == submission.BaseBranch)
            {
                throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
            }
            SourceControlInputPolicy.ValidateBranch(saved.Receipt.WorkBranch);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or NullReferenceException)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
        }
        var accepted = new AcceptedPullRequest(1, access.Revision, source.Id, saved.Receipt.WorkBranch,
            submission.BaseBranch, saved.Receipt.Commit.Value, submission.Title, submission.Description);
        var id = await EnqueueAsync(context, repositoryId, copiedRoles, SourceControlOperationKind.PullRequest,
            key, JsonSerializer.SerializeToUtf8Bytes(accepted), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    internal async Task<AcceptedPullRequestWork> ReadPullRequestWorkAsync(SourceControlContext context, Guid id,
        string worker, long fence, IReadOnlyCollection<string> currentRoles, CancellationToken cancellationToken)
    {
        Relational();
        var roles = currentRoles.ToArray();
        var now = DateTimeOffset.UtcNow.UtcTicks;
        var row = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id
            && x.TenantId == context.TenantId && x.ActorId == context.ActorId && x.LeaseOwner == worker
            && x.Fence == fence && x.LeaseUntilUtcTicks > now && x.Kind == (int)SourceControlOperationKind.PullRequest
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling),
            cancellationToken) ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        var access = await FindAsync(context.TenantId, row.RepositoryId, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        RepositoryAccessPolicy.Demand(context, access.Binding, roles, access.Grants, RepositoryPermission.PullRequest);
        try
        {
            var bytes = Unprotect(context, row.RepositoryId, row.ProtectedRequest);
            var accepted = JsonSerializer.Deserialize<AcceptedPullRequest>(bytes);
            if (accepted is null || accepted.SchemaVersion != 1 || accepted.PushOperationId == Guid.Empty
                || string.IsNullOrWhiteSpace(accepted.Title) || accepted.Title.Length > 256
                || accepted.Title.Any(char.IsControl) || accepted.Description is null || accepted.Description.Length > 16_384
                || accepted.WorkBranch is null || !accepted.WorkBranch.StartsWith("vertex/", StringComparison.Ordinal)
                || accepted.WorkBranch == accepted.BaseBranch || access.Binding.Remote.Host != "github.com"
                || accepted.BaseBranch != access.Binding.DefaultBranch && accepted.BaseBranch != access.Binding.ReleaseBranch)
            {
                throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
            }
            if (accepted.BindingRevision != access.Revision || RequestHash(access.Revision, bytes) != row.RequestHash)
            {
                throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
            }
            SourceControlInputPolicy.ValidateBranch(accepted.WorkBranch);
            SourceControlInputPolicy.ValidateBranch(accepted.BaseBranch);
            return new(access.Binding, access.Revision, accepted.PushOperationId,
                new(id, new(row.IdempotencyKey), accepted.WorkBranch, accepted.BaseBranch, new(accepted.HeadCommit),
                    accepted.Title, accepted.Description), (SourceControlOperationState)row.State);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or NullReferenceException)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
        }
    }

    internal async Task<bool> SavePullRequestIntentAsync(SourceControlContext context, Guid id, string worker,
        long fence, IReadOnlyCollection<string> currentRoles, CancellationToken cancellationToken)
    {
        var work = await ReadPullRequestWorkAsync(context, id, worker, fence, currentRoles, cancellationToken);
        if (work.State != SourceControlOperationState.Running)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
        }
        return await SaveResultAsync(context.TenantId, id, worker, fence,
            JsonSerializer.SerializeToUtf8Bytes(new StoredPullRequest(1, work.Binding.Id, work.Command, null)),
            DateTimeOffset.UtcNow, cancellationToken);
    }

    internal async Task<PullRequestReceipt> InvokePullRequestRemoteAsync(SourceControlContext context, Guid id,
        string worker, long fence, IReadOnlyCollection<string> roles,
        Func<AcceptedPullRequestWork, CancellationToken, Task<PullRequestReceipt>> invoke, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var work = await ReadPullRequestWorkAsync(context, id, worker, fence, roles, cancellationToken);
        if (await db.SourceControlBindings.Where(x => x.Id == work.Binding.Id && x.TenantId == context.TenantId
            && x.Revision == work.BindingRevision).ExecuteUpdateAsync(update => update.SetProperty(x => x.Revision, x => x.Revision), cancellationToken) != 1)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
        }
        var now = DateTimeOffset.UtcNow.UtcTicks;
        if (await db.SourceControlOperations.Where(x => x.Id == id && x.TenantId == context.TenantId
            && x.ActorId == context.ActorId && x.LeaseOwner == worker && x.Fence == fence && x.LeaseUntilUtcTicks > now
            && x.ProtectedResult != null && x.Kind == (int)SourceControlOperationKind.PullRequest
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Fence, x => x.Fence), cancellationToken) != 1)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
        }
        var until = await db.SourceControlOperations.Where(x => x.Id == id).Select(x => x.LeaseUntilUtcTicks).SingleAsync(cancellationToken);
        var remaining = new DateTimeOffset(until!.Value, TimeSpan.Zero) - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(remaining);
        var receipt = await invoke(work, deadline.Token);
        await transaction.CommitAsync(deadline.Token);
        return receipt;
    }

    internal async Task CompletePullRequestAsync(SourceControlContext context, Guid id, string worker, long fence,
        IReadOnlyCollection<string> currentRoles, PullRequestReceipt receipt, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var work = await ReadPullRequestWorkAsync(context, id, worker, fence, currentRoles, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var bytes = await ReadSavedResultAsync(context.TenantId, id, worker, fence, now, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
        StoredPullRequest? intent;
        try
        {
            intent = JsonSerializer.Deserialize<StoredPullRequest>(bytes);
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
        }
        if (intent is null || intent.SchemaVersion != 1 || intent.RepositoryId != work.Binding.Id
            || intent.Command != work.Command || intent.Receipt is not null
            || receipt.OperationId != id || receipt.HeadCommit != work.Command.HeadCommit || receipt.ProviderId != "github"
            || receipt.Number <= 0 || receipt.Url is null)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
        }
        var repository = work.Binding.Remote.AbsolutePath.Trim('/');
        if (repository.EndsWith(".git", StringComparison.Ordinal)) repository = repository[..^4];
        var expectedUrl = new Uri($"https://github.com/{repository}/pull/{receipt.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        if (receipt.Url != expectedUrl || !Enum.IsDefined(receipt.State)
            || receipt.State == PullRequestState.Merged && receipt.MergeCommit is null
            || receipt.State != PullRequestState.Merged && receipt.MergeCommit is not null)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
        }
        var payload = Protect(context, work.Binding.Id,
            JsonSerializer.SerializeToUtf8Bytes(intent with { Receipt = receipt }));
        var changed = await db.SourceControlOperations.Where(x => x.Id == id && x.TenantId == context.TenantId
            && x.ActorId == context.ActorId && x.LeaseOwner == worker && x.Fence == fence && x.LeaseUntilUtcTicks > now.UtcTicks
            && x.Kind == (int)SourceControlOperationKind.PullRequest
            && (x.State == (int)SourceControlOperationState.Running || x.State == (int)SourceControlOperationState.Reconciling))
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.ProtectedResult, payload), cancellationToken);
        if (changed != 1 || !await FinishAsync(context.TenantId, id, worker, fence,
            SourceControlOperationState.Succeeded, now, cancellationToken))
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private sealed record AcceptedPullRequest(int SchemaVersion, long BindingRevision, Guid PushOperationId,
        string WorkBranch, string BaseBranch, string HeadCommit, string Title, string Description);
}
