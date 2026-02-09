using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

public abstract class ConversationNode : InteractionNode
{
    public string? Name { get; set; }
    public IReadOnlyList<Participant> Participants { get; } = [];
}