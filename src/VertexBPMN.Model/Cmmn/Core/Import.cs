namespace VertexBPMN.Domain.Model.Cmmn.Core;

/// <summary>
/// Import for external definitions (5.1.3, inherits from CMMNElement).
/// </summary>
public record Import(
    string ImportType,
    string Location,
    string Namespace
) : CMMNElement();