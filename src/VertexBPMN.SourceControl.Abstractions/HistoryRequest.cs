namespace VertexBPMN.SourceControl.Abstractions;
public sealed record HistoryRequest(string Path, GitCommitId Commit, int PageSize, string? Cursor);
