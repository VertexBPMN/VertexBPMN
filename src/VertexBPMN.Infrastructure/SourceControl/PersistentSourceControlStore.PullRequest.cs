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
        if ((access.Binding.ConnectionProfileId is null && access.Binding.Remote.Host != "github.com")
            || (submission.BaseBranch != access.Binding.DefaultBranch && submission.BaseBranch != access.Binding.ReleaseBranch))
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
        }
        var source = await db.SourceControlOperations.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == submission.PushOperationId && x.RepositoryId == repositoryId && x.TenantId == context.TenantId
            && x.ActorId == context.ActorId && x.Kind == (int)SourceControlOperationKind.Push
            && x.State == (int)SourceControlOperationState.Pushed, cancellationToken)
            ?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
        StoredRemotePush saved;
        await DemandWriteMetadataAsync(context, source, access, cancellationToken);
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
        var hosting = access.Binding.ConnectionProfileId is null ? null
            : await CaptureHostingExecutionAsync(context, access, copiedRoles, cancellationToken);
        var accepted = new AcceptedPullRequest(hosting is null ? 1 : 2, access.Revision, source.Id, saved.Receipt.WorkBranch,
            submission.BaseBranch, saved.Receipt.Commit.Value, submission.Title, submission.Description,
            hosting is null ? null : HostingFingerprint(hosting));
        var request = JsonSerializer.SerializeToUtf8Bytes(accepted);
        var id = hosting is null
            ? await EnqueueAsync(context, repositoryId, copiedRoles, SourceControlOperationKind.PullRequest, key, request, cancellationToken)
            : await InsertOrReconcileAsync(context, repositoryId, SourceControlOperationKind.PullRequest, key,
                RequestHash(access.Revision, request), Protect(context, repositoryId, request), null, cancellationToken,
                hosting with { RequestDigest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(request)) });
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
        AcceptedHostingExecution? hosting = null;
        if (row.ExecutionContractVersion == AcceptedHostingExecution.ContractVersion)
        {
            hosting = await DemandHostingRowAsync(context, row, roles, cancellationToken);
        }
        else
        {
            DemandLegacyExecutionContract(row);
            DemandLegacyBinding(access.Binding);
        }
        try
        {
            var bytes = Unprotect(context, row.RepositoryId, row.ProtectedRequest);
            var accepted = JsonSerializer.Deserialize<AcceptedPullRequest>(bytes);
            if (accepted is null || accepted.SchemaVersion != (hosting is null ? 1 : 2) || accepted.PushOperationId == Guid.Empty
				|| string.IsNullOrWhiteSpace(accepted.Title) || accepted.Title.Length > 256
				|| accepted.Title.Any(char.IsControl) || accepted.Description is null || accepted.Description.Length > 16_384
				|| accepted.WorkBranch?.StartsWith("vertex/", StringComparison.Ordinal) != true
				|| accepted.WorkBranch == accepted.BaseBranch || (hosting is null && access.Binding.Remote.Host != "github.com")
                || (accepted.BaseBranch != access.Binding.DefaultBranch && accepted.BaseBranch != access.Binding.ReleaseBranch))
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
                    accepted.Title, accepted.Description), (SourceControlOperationState)row.State, hosting);
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
            JsonSerializer.SerializeToUtf8Bytes(new StoredPullRequest(work.Hosting is null ? 1 : 2,
                work.Binding.Id, work.Command, null, work.Hosting?.HostingTarget)),
            DateTimeOffset.UtcNow, cancellationToken);
    }

    internal async Task<PullRequestReceipt> InvokePullRequestRemoteAsync(SourceControlContext context, Guid id,
        string worker, long fence, IReadOnlyCollection<string> roles,
        Func<AcceptedPullRequestWork, CancellationToken, Task<PullRequestReceipt>> invoke, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var work = await ReadPullRequestWorkAsync(context, id, worker, fence, roles, cancellationToken);
        if (work.Hosting is not null)
        {
            await LockHostingProfileAsync(context, work.Hosting, cancellationToken);
        }
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
        if (remaining <= TimeSpan.Zero)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
		}

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
        if (intent is null || intent.SchemaVersion != (work.Hosting is null ? 1 : 2) || intent.RepositoryId != work.Binding.Id
            || intent.Command != work.Command || intent.Receipt is not null || intent.HostingTarget != work.Hosting?.HostingTarget)
        {
            throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
        }
        RepositoryHostingReceiptPolicy.Demand(work.Hosting?.HostingTarget ?? RepositoryHostingTargetPolicy.LegacyGitHub(work.Binding), work.Command, receipt);
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
        string WorkBranch, string BaseBranch, string HeadCommit, string Title, string Description,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ExecutionFingerprint = null);
}
