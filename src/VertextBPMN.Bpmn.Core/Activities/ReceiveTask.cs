using VertexBPMN.Domain.Model.Bpmn.Common.Messages;

namespace VertexBPMN.Domain.Model.Bpmn.Activities;

public class ReceiveTask : Task
{
    public Message? MessageRef { get; set; }
    public bool? Instantiate { get; set; }
}