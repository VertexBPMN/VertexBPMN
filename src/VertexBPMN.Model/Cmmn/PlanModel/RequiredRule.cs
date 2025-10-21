using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Required rule (5.4.11.2, inherits from CMMNElement).
/// </summary>
public record RequiredRule(
    Expression? Condition = null
) : CMMNElement();