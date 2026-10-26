namespace VertexBPMN.Sdk;

/// <summary>Status projection of a release activation operation (LC10).</summary>
public sealed record ApplicationReleaseStatus(
    string OperationId,
    string ApplicationDefinitionId,
    string ApplicationRevisionId,
    string State,
    string? BoundProcessDefinitionId,
    string? Error,
    object? Diagnostics,
    long PointerRevision,
    long PreparedAt,
    long ActivatedAt,
    long UpdatedAt);
