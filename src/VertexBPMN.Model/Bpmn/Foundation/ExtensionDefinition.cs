using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Bpmn.Foundation;

#nullable enable

/// <summary>
/// Extension definition, as per Figure 8.6.
/// </summary>
public record ExtensionDefinition(
    string Name,
    List<ExtensionAttributeDefinition> AttributeDefinitions
);