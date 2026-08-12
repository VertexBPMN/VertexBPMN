using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

public class ConversationLink : BaseElement
{
    public ConversationNode? SourceRef { get; set; }
    public ConversationNode? TargetRef { get; set; }
}