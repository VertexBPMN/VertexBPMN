namespace VertexBPMN.SourceControl.Abstractions;

public sealed record DeploymentCommand(Guid OperationId, SourceControlIdempotencyKey IdempotencyKey,
    Guid RepositoryId, GitCommitId Commit, string Path, string ContentSha256,
    string TargetId, string DefinitionName);
