using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

#nullable enable

/// <summary>
/// Conversation link, as per Figure 9.1.
/// </summary>
public record ConversationLink(
    ConversationNode SourceRef,
    ConversationNode TargetRef,
    string? Name = null
) : BaseElement;