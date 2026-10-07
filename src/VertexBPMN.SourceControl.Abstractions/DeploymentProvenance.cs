namespace VertexBPMN.SourceControl.Abstractions;
public sealed record DeploymentProvenance(Guid OperationId, SourceControlIdempotencyKey IdempotencyKey,
    string TenantId, string ActorId,
    Guid RepositoryId, GitCommitId Commit, string Path, string ContentSha256,
    string TargetId, Guid DefinitionId, int DefinitionVersion, ReleaseEvidence Evidence,
    DateTimeOffset DeployedAt);
