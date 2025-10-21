using VertexBPMN.Domain.Model.Cmmn.Core;
using VertexBPMN.Domain.Model.Cmmn.InformationModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Case parameter (5.4.10.3, inherits from CMMNElement).
/// </summary>
public record CaseParameter(
    CaseFileItem? BindingRef = null,
    Expression? BindingRefinement = null
) : CMMNElement();