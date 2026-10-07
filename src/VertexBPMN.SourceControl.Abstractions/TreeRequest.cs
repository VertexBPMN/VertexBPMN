namespace VertexBPMN.SourceControl.Abstractions;
public sealed record TreeRequest(GitCommitId Commit, string Root, int PageSize, string? Cursor);
