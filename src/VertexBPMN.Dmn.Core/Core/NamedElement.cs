namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>NamedElement (6.3.1)</summary>
public abstract class NamedElement : DMNElement
{
    public string Name { get; set; } = string.Empty;
}