namespace VertexBPMN.SourceControl.Abstractions;
public sealed record CommitSummary(GitCommitId Commit, string Subject, DateTimeOffset CommittedAt);
