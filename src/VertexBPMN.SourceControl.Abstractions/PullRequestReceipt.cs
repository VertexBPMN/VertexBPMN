namespace VertexBPMN.SourceControl.Abstractions;
public sealed record PullRequestReceipt(Guid OperationId, string ProviderId, long Number, Uri Url,
    PullRequestState State, GitCommitId HeadCommit, GitCommitId? MergeCommit)
{
    /// <summary>Null means reviews have not been checked; an empty collection means no reviews.</summary>
    public IReadOnlyList<PullRequestReview>? Reviews { get; init; }
	[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyList<PullRequestVote>? CurrentVotes { get; init; }
	[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
	public IReadOnlyList<PullRequestIteration>? Iterations { get; init; }
}
