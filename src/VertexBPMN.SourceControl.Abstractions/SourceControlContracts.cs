namespace VertexBPMN.SourceControl.Abstractions;

[Flags]
public enum RepositoryPermission
{
    None = 0,
    Read = 1,
    Commit = 2,
    Push = 4,
    PullRequest = 8,
    Deploy = 16,
    Manage = 32
}

[Flags]
public enum SourceControlCapability
{
    None = 0,
    Read = 1,
    Commit = 2,
    Push = 4,
    PullRequest = 8,
    VerifyRelease = 16,
    DeleteFile = 32,
    RenameFile = 64
}

/// <summary>Resolved server-side binding. CredentialReference is an ID, never a token or password.</summary>
public sealed record RepositoryBinding(Guid Id, string TenantId, Uri Remote,
    string? CredentialReference, string DefaultBranch, string ReleaseBranch,
    IReadOnlyList<string> ModelRoots);

public sealed record RepositoryGrant(string ActorId, RepositoryPermission Permissions);
public sealed record SourceControlAvailability(bool Available, SourceControlCapability Capabilities,
    SourceControlErrorCode? UnavailableReason);
public sealed record RevisionSelection(Guid RepositoryId, string Branch, GitCommitId Commit);
public sealed record EditSession(Guid Id, Guid RepositoryId, string TenantId, string ActorId,
    string WorkBranch, GitCommitId BaseCommit, Guid DocumentGeneration,
    long LocalRevision, long? CommittedRevision, DateTimeOffset ExpiresAt);
public sealed record RepositoryFile(string Path, SourceModelKind Kind, long ByteLength);
public sealed record CommitSummary(GitCommitId Commit, string Subject, DateTimeOffset CommittedAt);
public sealed record SourceControlPage<T>(IReadOnlyList<T> Items, string? NextCursor);
public sealed record FileReadRequest(GitCommitId Commit, string Path);
public sealed record HistoryRequest(string Path, GitCommitId Commit, int PageSize, string? Cursor);
public sealed record TreeRequest(GitCommitId Commit, string Root, int PageSize, string? Cursor);
public sealed record DiffRequest(GitCommitId BaseCommit, ModelSnapshot Snapshot);
public sealed record ModelDiff(string Path, string UnifiedText, bool Truncated);

public sealed record CommitCommand(Guid OperationId, SourceControlIdempotencyKey IdempotencyKey,
    Guid SessionId, GitCommitId BaseCommit, string WorkBranch, string Message,
    IReadOnlyList<ModelSnapshot> Snapshots);
public sealed record CommitReceipt(Guid OperationId, Guid SessionId, GitCommitId Commit,
    string WorkBranch, IReadOnlyList<CommittedSnapshot> Snapshots);
public sealed record CommittedSnapshot(string Path, Guid DocumentGeneration,
    long LocalRevision, string ContentSha256);

public sealed record PushCommand(Guid OperationId, SourceControlIdempotencyKey IdempotencyKey,
    string WorkBranch, GitCommitId Commit, ExpectedRemoteRef ExpectedRemote);
public sealed record PushReceipt(Guid OperationId, GitCommitId Commit, string WorkBranch);

public sealed record PullRequestCommand(Guid OperationId, SourceControlIdempotencyKey IdempotencyKey,
    string WorkBranch, string BaseBranch, GitCommitId HeadCommit, string Title, string Description);
public enum PullRequestState { Open, Closed, Merged }
public sealed record PullRequestReceipt(Guid OperationId, string ProviderId, long Number, Uri Url,
    PullRequestState State, GitCommitId HeadCommit, GitCommitId? MergeCommit);

/// <summary>
/// Evidence returned only by the trusted hosting adapter, never accepted as client-supplied proof.
/// The application revalidates permissions and evidence before deployment.
/// </summary>
public sealed record ReleaseEvidence(Guid RepositoryId, string ProviderId, long PullRequestNumber,
    string ReleaseBranch, GitCommitId HeadCommit, GitCommitId MergeCommit,
    DateTimeOffset MergedAt, DateTimeOffset VerifiedAt, string ProtectionFingerprint);

public enum SourceControlOperationKind { OpenSession, Commit, Push, PullRequest, Deploy }
public enum SourceControlOperationState
{
    Queued, Running, CommittedLocal, Pushed, Succeeded, Conflict, Failed,
    Cancelled, ResultUnknown, Reconciling
}
public sealed record SourceControlOperation(Guid Id, string TenantId, string ActorId,
    Guid RepositoryId, SourceControlOperationKind Kind, SourceControlOperationState State,
    DateTimeOffset UpdatedAt, SourceControlErrorCode? Error);

public sealed record DeploymentCommand(Guid OperationId, SourceControlIdempotencyKey IdempotencyKey,
    Guid RepositoryId, GitCommitId Commit, string Path, string ContentSha256,
    string TargetId, string DefinitionName);
public sealed record DeploymentProvenance(Guid OperationId, SourceControlIdempotencyKey IdempotencyKey,
    string TenantId, string ActorId,
    Guid RepositoryId, GitCommitId Commit, string Path, string ContentSha256,
    string TargetId, Guid DefinitionId, int DefinitionVersion, ReleaseEvidence Evidence,
    DateTimeOffset DeployedAt);

/// <summary>Stable public codes; exception messages/stdout/stderr must not be returned to clients.</summary>
public enum SourceControlErrorCode
{
    Unauthenticated, Forbidden, NotFound, RevisionConflict, IdempotencyConflict,
    InvalidInput, PayloadTooLarge, QuotaExceeded, Disabled, GitUnavailable,
    ProviderUnavailable, CredentialUnavailable, ReleaseNotApproved,
    ContentUnsafe, TimedOut, Cancelled, ResultUnknown
}
