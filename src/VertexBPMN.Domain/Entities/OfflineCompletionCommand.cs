namespace VertexBPMN.Domain.Entities;

/// <summary>
/// One queued offline task completion, carried by the client and replayed to the server
/// after reconnect (LC11 / LC-AC-19). Every item is bound to the task snapshot the client
/// prepared offline: expected revision + frozen form schema hash + a client idempotency key.
/// "Offline" here means prepared locally - it is never a server-side claim or free start.
/// </summary>
public sealed record OfflineCompletionCommand(
    Guid TaskId, IDictionary<string, object>? Variables, string? IdempotencyKey,
    long? ExpectedRevision, string? FormSchemaHash);
