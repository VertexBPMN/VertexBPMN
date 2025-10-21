using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// InputData (Figure 6-17, extends DRGElement).
/// </summary>
public record InputData(
    InformationItem Variable
) : DRGElement();