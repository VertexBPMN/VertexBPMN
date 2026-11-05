using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Replays a client's offline completion queue against the current server state (LC11 / LC-AC-19).
/// Each queued completion is re-validated server-side (identity, tenant, permission, task revision,
/// frozen form schema hash) and applied exactly-once via the idempotent completion contract. Stale,
/// revoked or conflicting items are reported as visible conflicts per item - never silently
/// overwritten, and never re-executed a second time.
/// </summary>
public interface IOfflineCompletionReplayService
{
    Task<IReadOnlyList<OfflineCompletionReplayResult>> ReplayAsync(
        IReadOnlyList<OfflineCompletionCommand> completions,
        string? userId, string? tenantId, bool isAdmin, IReadOnlyCollection<string>? roles,
        CancellationToken ct = default);
}
