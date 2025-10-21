using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Manual activation rule (5.4.11.3, inherits from CMMNElement).
/// </summary>
public record ManualActivationRule(
    Expression? Condition = null
) : CMMNElement();