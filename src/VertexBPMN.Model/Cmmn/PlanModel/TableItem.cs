using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Abstract table item (5.4.9.1.1, inherits from CMMNElement).
/// </summary>
public abstract record TableItem(
    List<ApplicabilityRule> ApplicabilityRuleRefs = null!
) : CMMNElement();