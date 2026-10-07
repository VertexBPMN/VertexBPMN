namespace VertexBPMN.SourceControl.Abstractions;
public sealed record FileReadRequest(GitCommitId Commit, string Path);
