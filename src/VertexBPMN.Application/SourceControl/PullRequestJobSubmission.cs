namespace VertexBPMN.Application.SourceControl;

/// <summary>PR source is an owned confirmed push, not a caller-selected branch or commit.</summary>
public sealed record PullRequestJobSubmission(Guid PushOperationId, string BaseBranch, string Title, string Description);
