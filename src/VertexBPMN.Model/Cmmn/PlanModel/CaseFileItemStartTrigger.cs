using VertexBPMN.Domain.Model.Cmmn.InformationModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Case file item start trigger (Figure 5.7, inherits from StartTrigger).
/// </summary>
public record CaseFileItemStartTrigger(
    CaseFileItemTransition StandardEvent,
    CaseFileItem SourceRef
) : StartTrigger();