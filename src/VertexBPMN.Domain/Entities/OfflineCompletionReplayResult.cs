namespace VertexBPMN.Domain.Entities;

/// <summary>Per-item outcome of an offline completion replay.</summary>
public sealed record OfflineCompletionReplayResult(
    Guid TaskId, OfflineCompletionStatus Status, string? ConflictCode = null,
    long? Revision = null, string? FormSchemaHash = null);
