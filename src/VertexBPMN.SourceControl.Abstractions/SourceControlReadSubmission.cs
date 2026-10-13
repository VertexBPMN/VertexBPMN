namespace VertexBPMN.SourceControl.Abstractions;

/// <summary>Host-constructed read submission. HTTP adapters must decode snapshots through their bounded byte DTO.</summary>
public sealed record SourceControlReadSubmission(GitReadQueryKind Kind, GitCommitId? Revision, string? Path,
	int PageSize, string? Cursor, Guid Generation, ModelSnapshot? Snapshot = null);
