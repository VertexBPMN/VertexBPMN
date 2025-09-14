namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Resilience status information
/// </summary>
public record ResilienceStatus(
    string OperationName,
    string Status,
    string Message
);