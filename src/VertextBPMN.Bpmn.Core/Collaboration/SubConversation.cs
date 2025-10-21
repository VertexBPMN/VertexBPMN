using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

public class SubConversation : ConversationNode
{
    public IReadOnlyList<ConversationNode> ConversationNodes { get; } = [];
}