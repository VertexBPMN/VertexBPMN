namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Persistent activation lifecycle of one application release generation (LC10).
/// The state machine is durable in the DB (not a distributed SQL transaction): the
/// external deployment step is recorded as <see cref="DeploymentRunning"/> and later
/// reconciled to <see cref="Ready"/> / <see cref="Active"/> / <see cref="Failed"/> /
/// <see cref="Recovered"/> so response loss and crashes are absorbed.
/// </summary>
public enum ApplicationReleaseState
{
    /// <summary>Manifest + reference graph validated and stored; not yet deployed.</summary>
    Prepared,

    /// <summary>API-deploy / release-deploy orchestration is in flight.</summary>
    DeploymentRunning,

    /// <summary>Deployment observed as complete; pointer not yet switched.</summary>
    Ready,

    /// <summary>The pointer has been atomically switched to this release; it is the active generation.</summary>
    Active,

    /// <summary>Deployment or pointer switch failed and is visible to users as such.</summary>
    Failed,

    /// <summary>A previously failed/incomplete activation was reconciled back to a consistent state.</summary>
    Recovered,
}
