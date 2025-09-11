namespace VertexBPMN.Core.Modeling;

/// <summary>
/// Context for compensation handling in transaction subprocesses
/// </summary>
public record CompensationContext(string EventId, string AttachedActivityId);