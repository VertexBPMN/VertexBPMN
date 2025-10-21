using VertexBPMN.Domain.Model.Bpmn.Common.Items;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Messages;

public class Message : RootElement
{
    public string? Name { get; set; }
    public ItemDefinition? ItemRef { get; set; }
}