namespace VertexBPMN.Domain.Interfaces;

public interface ITaskCompletionService
{
    ValueTask CompleteValidatedAsync(Guid taskId, IDictionary<string, object>? variables, string? idempotencyKey,
        long? expectedRevision, string? formSchemaHash, string? userId, string? tenantId, bool isAdmin,
        IReadOnlyCollection<string>? roles = null, CancellationToken ct = default);
}
