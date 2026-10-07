namespace VertexBPMN.SourceControl.Abstractions;
public sealed record DiffRequest(GitCommitId BaseCommit, ModelSnapshot Snapshot);
