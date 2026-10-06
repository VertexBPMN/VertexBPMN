namespace VertexBPMN.SourceControl.Abstractions;
public sealed record SourceControlOperation(Guid Id, string TenantId, string ActorId,
    Guid RepositoryId, SourceControlOperationKind Kind, SourceControlOperationState State,
    DateTimeOffset UpdatedAt, SourceControlErrorCode? Error);
