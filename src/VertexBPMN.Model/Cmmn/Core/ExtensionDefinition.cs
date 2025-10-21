namespace VertexBPMN.Domain.Model.Cmmn.Core;

/// <summary>
/// Extension definition for extensibility (5.1.5.2, inherits from CMMNElement).
/// </summary>
public record ExtensionDefinition(
    string Name,
    List<ExtensionAttributeDefinition> ExtensionAttributeDefinitions = null!
) : CMMNElement();