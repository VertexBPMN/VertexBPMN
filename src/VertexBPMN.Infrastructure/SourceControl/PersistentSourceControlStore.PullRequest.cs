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

    private sealed record AcceptedPullRequest(int SchemaVersion, long BindingRevision, Guid PushOperationId,
        string WorkBranch, string BaseBranch, string HeadCommit, string Title, string Description);
}
