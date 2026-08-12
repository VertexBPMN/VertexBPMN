using VertexBPMN.Domain.Model.Bpmn.Common.Items;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Events;

public class Signal : RootElement
{
    public string? Name { get; set; }
    public ItemDefinition? StructureRef { get; set; }
}