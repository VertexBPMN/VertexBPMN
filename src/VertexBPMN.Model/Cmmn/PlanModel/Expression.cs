using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Expression for conditions (inherits from CMMNElement).
/// </summary>
public record Expression(
    string? Body = null,
    string? Language = null
) : CMMNElement();