using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Decision (inherits from CMMNElement).
/// </summary>
public record Decision(
    string ImplementationType
) : CMMNElement();