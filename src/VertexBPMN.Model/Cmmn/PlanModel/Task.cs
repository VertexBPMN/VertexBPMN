namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Abstract task (5.4.9, inherits from PlanItemDefinition).
/// Extension: Added integration refs.
/// </summary>
public abstract record Task(
    bool IsBlocking = true,
    List<CaseParameter> Inputs = null!,
    List<CaseParameter> Outputs = null!,
    List<ParameterMapping> Mappings = null! // Extension: For BPMN/DMN data flow.
) : PlanItemDefinition(Name: string.Empty);