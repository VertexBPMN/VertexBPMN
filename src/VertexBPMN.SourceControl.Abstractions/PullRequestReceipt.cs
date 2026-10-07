namespace VertexBPMN.SourceControl.Abstractions;
public sealed record PullRequestReceipt(Guid OperationId, string ProviderId, long Number, Uri Url,
    PullRequestState State, GitCommitId HeadCommit, GitCommitId? MergeCommit);
