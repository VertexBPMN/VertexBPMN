namespace VertexBPMN.SourceControl.Abstractions;
public sealed record RevisionSelection(Guid RepositoryId, string Branch, GitCommitId Commit);
