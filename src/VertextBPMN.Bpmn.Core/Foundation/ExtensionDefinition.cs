using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Bpmn.Foundation;

public class ExtensionDefinition : BaseElement
{
    public string? Name { get; set; }
    public IReadOnlyList<ExtensionAttributeDefinition> Attributes { get; } = [];
}