namespace VertexBPMN.SourceControl.Abstractions;
public sealed record PushReceipt(Guid OperationId, GitCommitId Commit, string WorkBranch);
