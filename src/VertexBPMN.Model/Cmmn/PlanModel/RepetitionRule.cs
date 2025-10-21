using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Repetition rule (5.4.11.1, inherits from CMMNElement).
/// </summary>
public record RepetitionRule(
    Expression? Condition = null
) : CMMNElement();