namespace VertexBPMN.Domain.Entities;

public sealed record TaskCompletionCommand(Guid TaskId, IDictionary<string, object>? Variables, string? IdempotencyKey,
    long? ExpectedRevision, string? FormSchemaHash, string? UserId, string? TenantId, bool IsAdmin, IReadOnlyCollection<string>? Roles = null);
