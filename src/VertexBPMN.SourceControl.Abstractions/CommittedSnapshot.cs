namespace VertexBPMN.SourceControl.Abstractions;
public sealed record CommittedSnapshot(string Path, Guid DocumentGeneration,
    long LocalRevision, string ContentSha256);
