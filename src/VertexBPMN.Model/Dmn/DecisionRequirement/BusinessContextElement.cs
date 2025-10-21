using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionRequirement;

/// <summary>
/// Abstract BusinessContextElement (Figure 6-14, extends NamedElement).
/// </summary>
public abstract record BusinessContextElement(
    string? Uri = null
) : NamedElement();