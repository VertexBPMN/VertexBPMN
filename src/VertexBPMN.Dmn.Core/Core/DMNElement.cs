using VertexBPMN.Domain.Model.Dmn.Extensibility;

namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>DMNElement (6.3.1)</summary>
public abstract class DMNElement
{
    public string? Id { get; set; }
    public string? Description { get; set; }
    public string? Label { get; set; }

    public ExtensionElements? ExtensionElements { get; set; }
    public List<ExtensionAttribute> ExtensionAttributes { get; } = new();
}