namespace VertexBPMN.Domain;

/// <summary>
/// Resilience status information
/// </summary>
public record ResilienceStatus(
    string OperationName,
    string Status,
    string Message
);