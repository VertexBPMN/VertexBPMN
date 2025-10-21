namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>
/// Information item (extends NamedElement).
/// </summary>
public record InformationItem(
    string TypeRef
) : NamedElement();