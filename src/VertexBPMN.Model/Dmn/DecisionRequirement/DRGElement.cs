using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// Abstract DRGElement (extends NamedElement).
/// </summary>
public abstract record DRGElement() : NamedElement();