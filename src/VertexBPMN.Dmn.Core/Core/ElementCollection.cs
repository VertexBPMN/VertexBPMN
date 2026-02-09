using VertexBPMN.Domain.Model.Dmn.DRD;

namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>ElementCollection (6.3.4)</summary>
public sealed class ElementCollection : NamedElement
{
    public List<DRGElement> DrgElements { get; } = new();
}