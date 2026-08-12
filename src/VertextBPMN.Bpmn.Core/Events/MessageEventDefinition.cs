using VertexBPMN.Domain.Model.Bpmn.Common.Messages;
using VertexBPMN.Domain.Model.Bpmn.Services;

namespace VertexBPMN.Domain.Model.Bpmn.Events;

public class MessageEventDefinition : EventDefinition
{
    public Message? MessageRef { get; set; }
    public Operation? OperationRef { get; set; }
}