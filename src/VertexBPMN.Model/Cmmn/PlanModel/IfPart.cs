using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// If part for sentry (Figure 5.9, inherits from CMMNElement).
/// </summary>
public record IfPart(
    Expression Condition
) : CMMNElement();