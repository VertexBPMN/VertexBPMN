using VertexBPMN.Domain.Model.Cmmn.InformationModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Case file item on part (Figure 5.9, inherits from OnPart).
/// </summary>
public record CaseFileItemOnPart(
    CaseFileItemTransition StandardEvent,
    CaseFileItem SourceRef
) : OnPart();