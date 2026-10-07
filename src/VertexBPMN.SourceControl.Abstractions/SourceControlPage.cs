namespace VertexBPMN.SourceControl.Abstractions;
public sealed record SourceControlPage<T>(IReadOnlyList<T> Items, string? NextCursor);
