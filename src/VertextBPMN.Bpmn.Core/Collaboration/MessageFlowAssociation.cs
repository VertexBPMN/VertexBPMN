using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

public class MessageFlowAssociation : BaseElement
{
    public MessageFlow? InnerMessageFlowRef { get; set; }
    public MessageFlow? OuterMessageFlowRef { get; set; }
}