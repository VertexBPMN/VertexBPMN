namespace VertexBPMN.SourceControl.Abstractions;
public sealed record CommitReceipt(Guid OperationId, Guid SessionId, GitCommitId Commit,
    string WorkBranch, IReadOnlyList<CommittedSnapshot> Snapshots);
