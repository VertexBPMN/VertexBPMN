using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// ElementCollection (extends NamedElement).
/// </summary>
public record ElementCollection(
    List<DRGElement> DrgElements = null!
) : NamedElement();