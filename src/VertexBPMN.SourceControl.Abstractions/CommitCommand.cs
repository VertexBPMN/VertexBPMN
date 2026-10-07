namespace VertexBPMN.SourceControl.Abstractions;

public sealed record CommitCommand(Guid OperationId, SourceControlIdempotencyKey IdempotencyKey,
    Guid SessionId, GitCommitId BaseCommit, string WorkBranch, string Message,
    IReadOnlyList<ModelSnapshot> Snapshots);
