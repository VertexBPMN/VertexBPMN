namespace VertexBPMN.SourceControl.Abstractions;
public sealed record EditSession(Guid Id, Guid RepositoryId, string TenantId, string ActorId,
    string WorkBranch, GitCommitId BaseCommit, Guid DocumentGeneration,
    long LocalRevision, long? CommittedRevision, DateTimeOffset ExpiresAt);
