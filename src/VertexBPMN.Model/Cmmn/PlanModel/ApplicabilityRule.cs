using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Applicability rule (5.4.9.1.1, inherits from CMMNElement).
/// </summary>
public record ApplicabilityRule(
    Expression Condition,
    PlanningTable? ContextRef = null
) : CMMNElement();