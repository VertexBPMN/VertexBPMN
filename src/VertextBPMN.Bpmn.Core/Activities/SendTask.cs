using VertexBPMN.Domain.Model.Bpmn.Common.Messages;

namespace VertexBPMN.Domain.Model.Bpmn.Activities;

public class SendTask : Task
{
    public Message? MessageRef { get; set; }
}