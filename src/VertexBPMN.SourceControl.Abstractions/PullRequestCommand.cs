namespace VertexBPMN.SourceControl.Abstractions;

public sealed record PullRequestCommand(Guid OperationId, SourceControlIdempotencyKey IdempotencyKey,
    string WorkBranch, string BaseBranch, GitCommitId HeadCommit, string Title, string Description);
