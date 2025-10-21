namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Plan item on part (Figure 5.9, inherits from OnPart).
/// </summary>
public record PlanItemOnPart(
    PlanItemTransition StandardEvent,
    PlanItem SourceRef
) : OnPart();