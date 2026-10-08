namespace VertexBPMN.SourceControl.Abstractions;
public sealed record PullRequestReceipt(Guid OperationId, string ProviderId, long Number, Uri Url,
    PullRequestState State, GitCommitId HeadCommit, GitCommitId? MergeCommit)
{
    /// <summary>Null means reviews have not been checked; an empty collection means no reviews.</summary>
    public IReadOnlyList<PullRequestReview>? Reviews { get; init; }
}
