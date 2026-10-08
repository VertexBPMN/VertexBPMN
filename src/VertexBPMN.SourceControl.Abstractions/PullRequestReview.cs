namespace VertexBPMN.SourceControl.Abstractions;

public sealed record PullRequestReview(long Id, string Reviewer, string State, GitCommitId Commit);
