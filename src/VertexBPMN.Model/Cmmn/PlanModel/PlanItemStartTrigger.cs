namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Plan item start trigger (Figure 5.7, inherits from StartTrigger).
/// </summary>
public record PlanItemStartTrigger(
    PlanItemTransition StandardEvent,
    PlanItem? SourceRef = null
) : StartTrigger();