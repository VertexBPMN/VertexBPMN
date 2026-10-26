using System.Text.Json;

namespace VertexBPMN.Domain.Entities;

/// <summary>
/// A single release generation of an application definition with a persistent activation
/// state machine (LC10). One row per prepared/activated release: it carries the validated
/// release manifest (schema-version, artifact-kind, key, revision, hash, dependencies,
/// target profile, credential alias), the reference graph + compatibility diagnostics, the
/// atomic active-pointer switch state, and the durable operation identity used for idempotent
/// retries and reconciliation.
///
/// Concurrency model: <see cref="PointerRevision"/> is an optimistic concurrency token that
/// guards the atomic, revision-checked switch of the active release pointer. Concurrent API
/// replicas read the same row and CAS on <see cref="PointerRevision"/> — the loser reconciles
/// by reloading, so a repeated activation request yields exactly one consistent activation.
/// </summary>
public sealed class ApplicationReleaseRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    /// <summary>Durable operation identity — reused on retries so duplicate requests are idempotent.</summary>
    public string OperationId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;
    public string ApplicationDefinitionId { get; set; } = string.Empty;
    public string ApplicationRevisionId { get; set; } = string.Empty;

    /// <summary>The full release manifest (schema-version, artifact-kind, key, revision, hash, dependencies, target profile, credential alias).</summary>
    public string ManifestJson { get; set; } = "{}";

    public ApplicationReleaseState State { get; set; } = ApplicationReleaseState.Prepared;

    /// <summary>Reference-graph / compatibility diagnostics; "[]" when clean. Missing/foreign artifacts set this instead of activating.</summary>
    public string DiagnosticsJson { get; set; } = "[]";

    /// <summary>Human-readable error of the last failed/reconciled attempt; empty when none.</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>Optimistic concurrency token guarding the atomic pointer switch.</summary>
    public long PointerRevision { get; set; }

    /// <summary>
    /// The generation's revision this release binds (used by the version-bound start entry).
    /// When a public start contract resolves key→latest, an additive version-bound entry is
    /// stored here so the concrete tenant-checked ProcessDefinitionId of THIS release wins.
    /// </summary>
    public string BoundProcessDefinitionId { get; set; } = string.Empty;

    /// <summary>Git provenance reused from the model source-control stack (G10 manifest); never mutated here.</summary>
    public string GitCommitSha { get; set; } = string.Empty;
    public string GitRepository { get; set; } = string.Empty;

    public long PreparedAtTicks { get; set; }
    public long DeployStartedAtTicks { get; set; }
    public long ActivatedAtTicks { get; set; }
    public long UpdatedAtTicks { get; set; }

    public ApplicationReleaseRecord CloneForIdempotentRetry() => new()
    {
        Id = Id,
        OperationId = OperationId,
        TenantId = TenantId,
        ApplicationDefinitionId = ApplicationDefinitionId,
        ApplicationRevisionId = ApplicationRevisionId,
        ManifestJson = ManifestJson,
        State = State,
        DiagnosticsJson = DiagnosticsJson,
        ErrorMessage = ErrorMessage,
        PointerRevision = PointerRevision,
        BoundProcessDefinitionId = BoundProcessDefinitionId,
        GitCommitSha = GitCommitSha,
        GitRepository = GitRepository,
        PreparedAtTicks = PreparedAtTicks,
        DeployStartedAtTicks = DeployStartedAtTicks,
        ActivatedAtTicks = ActivatedAtTicks,
        UpdatedAtTicks = UpdatedAtTicks,
    };

    public string ToStatusJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            Id,
            OperationId,
            TenantId,
            ApplicationDefinitionId,
            ApplicationRevisionId,
            State = State.ToString(),
            BoundProcessDefinitionId,
            Error = ErrorMessage,
            Diagnostics = DiagnosticsJson,
            PointerRevision,
            PreparedAt = PreparedAtTicks,
            DeployStartedAt = DeployStartedAtTicks,
            ActivatedAt = ActivatedAtTicks,
            UpdatedAt = UpdatedAtTicks,
            GitCommitSha,
            GitRepository,
        });
    }
}
