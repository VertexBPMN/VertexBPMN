namespace VertexBPMN.Domain.Model.Dmn.Core;

/// <summary>
/// Import.
/// </summary>
public record Import(
    string ImportType = "",
    string Namespace ="",
    string? LocationUri = null
) : NamedElement();