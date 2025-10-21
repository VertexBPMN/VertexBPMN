using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Bpmn.Foundation;

#nullable enable

/// <summary>
/// The base element for all BPMN elements, as per Figure 8.5.
/// </summary>
public abstract record BaseElement
{
    public string? Id { get; set; }
    public List<Documentation>? Documentation { get; set; } = [];
    public List<ExtensionDefinition>? ExtensionDefinitions { get; set; } = [];
    public ExtensionElements? ExtensionElements { get; set; }

}
