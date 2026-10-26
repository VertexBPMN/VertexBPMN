using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// LC10 release activation lifecycle: Validate → Prepare → Activate → Status → Rollback, with
/// a persistent state machine and durable-operation identity so retries are idempotent and
/// response loss / crashes are absorbed by reconciliation. Cleanup is a separately named
/// request so Rollback and Cleanup are isolated test-data operations.
/// </summary>
public interface IApplicationReleaseService
{
    /// <summary>Validates a release archive (hardened import) without persisting anything.</summary>
    Task<IReadOnlyList<string>> ValidateArchiveAsync(string tenantId, Stream archiveBytes, CancellationToken cancellationToken = default);

    /// <summary>Validates a release manifest + reference graph, storing the release as Prepared (never auto-activating).</summary>
    Task<ApplicationReleaseRecord> PrepareAsync(string operationId, string tenantId, string applicationDefinitionId, int revision, string manifestJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the API-deploy + release-deploy orchestration and, on success, atomically switches
    /// the active release pointer. Reuses the same operation identity on retries (idempotent).
    /// </summary>
    Task<ApplicationReleaseRecord> ActivateAsync(string operationId, string tenantId, string applicationDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>Returns the current durable state, reconciling any in-flight deployment.</summary>
    Task<ApplicationReleaseRecord?> GetStatusAsync(string tenantId, string applicationDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>Reverts the active pointer to a prior release generation (revision-checked; new starts only).</summary>
    Task<ApplicationReleaseRecord?> RollbackAsync(string operationId, string tenantId, string applicationDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>Isolated test-data cleanup: removes prepared/failed releases that never activated.</summary>
    Task CleanupAsync(string tenantId, string applicationDefinitionId, CancellationToken cancellationToken = default);

    /// <summary>Resolves the version-bound release start entry (concrete tenant-checked ProcessDefinitionId of THIS release).</summary>
    Task<string?> ResolveActiveProcessDefinitionIdAsync(string tenantId, string applicationDefinitionId, CancellationToken cancellationToken = default);
}
