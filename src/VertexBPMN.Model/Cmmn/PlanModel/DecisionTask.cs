namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Decision task (5.4.9.4, inherits from Task).
/// Extension: Enhanced DMN integration.
/// </summary>
public record DecisionTask(
    Decision DecisionRef // Ref to DMN Decision.
) : Task();