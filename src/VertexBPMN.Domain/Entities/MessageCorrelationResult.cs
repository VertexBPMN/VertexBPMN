namespace VertexBPMN.EngineServices;

/// <summary>
/// Result DTO for message correlation (Vertex compatibility).
/// </summary>
public record MessageCorrelationResult(string ResultType, string ExecutionId, string ProcessInstanceId, string ProcessDefinitionId);
