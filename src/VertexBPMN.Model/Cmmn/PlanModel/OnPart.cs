using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Abstract on part (Figure 5.9, inherits from CMMNElement).
/// </summary>
public abstract record OnPart(
    string? Name = null
) : CMMNElement();