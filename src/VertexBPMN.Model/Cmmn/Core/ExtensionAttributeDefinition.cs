namespace VertexBPMN.Domain.Model.Cmmn.Core;

/// <summary>
/// Extension attribute definition.
/// </summary>
public record ExtensionAttributeDefinition(
    string Name,
    string Type,
    bool IsReference = false
) : CMMNElement();