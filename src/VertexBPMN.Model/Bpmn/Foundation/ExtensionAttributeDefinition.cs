namespace VertexBPMN.Domain.Model.Bpmn.Foundation;

#nullable enable

/// <summary>
/// Extension attribute definition, as per Figure 8.6.
/// </summary>
public record ExtensionAttributeDefinition(
    string Name,
    string Type,
    bool IsReference = false
);