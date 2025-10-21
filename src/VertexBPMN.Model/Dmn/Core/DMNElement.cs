using VertexBPMN.Domain.Model.Dmn.Extensibility;

namespace VertexBPMN.Domain.Model.Dmn.Core;

#nullable enable

/// <summary>
/// Abstract superclass for DMN elements (Figure 6-11).
/// </summary>
public abstract record DMNElement
{
    public int MyProperty { get; set; }
    public string? Id { get; set; }
    public string? Description { get; set; }
    public string? Label { get; set; }
    public ExtensionElements? ExtensionElements { get; set; }
    public List<ExtensionAttribute>? ExtensionAttributes { get; set; } = [];
}
