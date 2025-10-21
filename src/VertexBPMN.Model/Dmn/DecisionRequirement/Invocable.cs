using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// Abstract Invocable (extends DRGElement).
/// </summary>
public abstract record Invocable(
    InformationItem Variable
) : DRGElement();