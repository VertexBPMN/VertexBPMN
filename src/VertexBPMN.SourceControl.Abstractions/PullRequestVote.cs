namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>A provider's current vote; no historical reviewed-commit or release approval is inferred.</summary>
public sealed record PullRequestVote(string ReviewerId, string Reviewer, int Vote);
