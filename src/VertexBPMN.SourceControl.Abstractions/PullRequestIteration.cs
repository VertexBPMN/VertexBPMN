namespace VertexBPMN.SourceControl.Abstractions;

public sealed record PullRequestIteration(long Id, GitCommitId SourceCommit, GitCommitId TargetCommit);
