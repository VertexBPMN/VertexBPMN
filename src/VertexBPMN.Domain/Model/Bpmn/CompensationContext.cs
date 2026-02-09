namespace VertexBPMN.Domain.Model.Bpmn;

/// <summary>
/// Context for compensation handling in transaction subprocesses
/// </summary>
public record CompensationContext(string EventId, string AttachedActivityId);