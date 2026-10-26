using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Exceptions;

/// <summary>
/// Raised when a release cannot be prepared/activated because the reference graph or
/// compatibility check failed, or the durable state machine is in a non-activatable state.
/// Carries the release row so the stalled/prepared/failed activation stays visible to users.
/// </summary>
public sealed class ApplicationReleaseCompatibilityException(string message, ApplicationReleaseRecord? release) : Exception(message)
{
    public ApplicationReleaseRecord? Release { get; } = release;
}
