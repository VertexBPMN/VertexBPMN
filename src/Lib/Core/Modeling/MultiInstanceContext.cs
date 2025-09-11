namespace VertexBPMN.Core.Modeling;

/// <summary>
/// Context for multi-instance loop execution
/// </summary>
public record MultiInstanceContext(string ActivityId, int TotalInstances, int CompletedInstances, bool IsSequential);