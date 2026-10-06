namespace VertexBPMN.SourceControl.Abstractions;

public sealed record PushCommand(Guid OperationId, SourceControlIdempotencyKey IdempotencyKey,
    string WorkBranch, GitCommitId Commit, ExpectedRemoteRef ExpectedRemote);
