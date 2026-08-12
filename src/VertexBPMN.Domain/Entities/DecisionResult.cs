namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Represents the result of a DMN decision evaluation.
/// </summary>
public record DecisionResult(IDictionary<string, object> Variables);
