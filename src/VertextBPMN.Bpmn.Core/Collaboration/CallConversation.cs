namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

public class CallConversation : ConversationNode
{
    public ConversationNode? CalledConversationRef { get; set; }
}