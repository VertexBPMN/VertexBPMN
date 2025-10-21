namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Abstract event listener (Figure 5.7, inherits from PlanItemDefinition).
/// </summary>
public abstract record EventListener() : PlanItemDefinition(Name: string.Empty);