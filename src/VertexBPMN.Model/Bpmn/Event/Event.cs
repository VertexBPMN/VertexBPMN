using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Process;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Abstract event, as per Figure 10.69.
/// </summary>
public abstract record Event() : FlowNode
{
   public List<Property>? Properties { get; set; } = [];
   public string Type { get; set; }
   public List<EventDefinition>? EventDefinitions { get; set; } = [];
}