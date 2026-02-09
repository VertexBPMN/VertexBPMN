using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Bpmn.Events;

public class LinkEventDefinition : EventDefinition
{
    public string? Name { get; set; }
    public IReadOnlyList<LinkEventDefinition> Sources { get; } = [];
    public LinkEventDefinition? Target { get; set; }
}