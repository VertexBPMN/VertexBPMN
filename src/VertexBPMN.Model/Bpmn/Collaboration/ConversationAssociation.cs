using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

#nullable enable

/// <summary>
/// Conversation association, as per Figure 9.31.
/// </summary>
public record ConversationAssociation(
    ConversationNode InnerConversationNodeRef,
    ConversationNode OuterConversationNodeRef
) : BaseElement;