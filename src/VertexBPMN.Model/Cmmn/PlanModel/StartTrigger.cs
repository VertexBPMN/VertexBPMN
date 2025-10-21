using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Abstract start trigger (Figure 5.7, inherits from CMMNElement).
/// </summary>
public abstract record StartTrigger(
    string? Name = null
) : CMMNElement();