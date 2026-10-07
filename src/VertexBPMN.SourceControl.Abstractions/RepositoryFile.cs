namespace VertexBPMN.SourceControl.Abstractions;
public sealed record RepositoryFile(string Path, SourceModelKind Kind, long ByteLength);
