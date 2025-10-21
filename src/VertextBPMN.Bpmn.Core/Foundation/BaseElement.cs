using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Bpmn.Foundation;

public abstract class BaseElement
{
    public string? Id { get; set; }
    public IReadOnlyList<Documentation> Documentation { get; } = [];
    public IReadOnlyList<ExtensionAttributeValue> ExtensionValues { get; } = [];
    public IReadOnlyList<ExtensionDefinition> ExtensionDefinitions { get; } = [];
}