using VertexBPMN.Domain.Entities.Modeling;

namespace VertexBPMN.Domain.Entities;
/// <summary>
/// Represents a decision evaluation instance for audit and history.
/// </summary>
public record DecisionInstance(
    string Id,
    string DecisionDefinitionKey,
    string? TenantId,
    DateTime EvaluationTime,
    IDictionary<string, object> InputVariables,
    IDictionary<string, object> OutputVariables,
    bool Failed = false,
    string? ErrorMessage = null);



