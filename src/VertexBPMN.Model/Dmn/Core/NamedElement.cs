namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>
/// Abstract superclass for named elements (extends DMNElement).
/// </summary>
public abstract record NamedElement : DMNElement
{
    public string Name { get; set; } = string.Empty;
}
