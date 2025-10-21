namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Parameter mapping for task integration (extension for BPMN/DMN).
/// </summary>
public record ParameterMapping(
    CaseParameter Source,
    CaseParameter Target,
    Expression? Transformation = null
);