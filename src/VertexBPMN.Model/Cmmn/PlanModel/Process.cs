using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

/// <summary>
/// Process (inherits from CMMNElement).
/// </summary>
public record Process(
    string ImplementationType
) : CMMNElement();