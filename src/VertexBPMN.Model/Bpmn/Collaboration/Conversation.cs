using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

#nullable enable

/// <summary>
/// Conversation class, as per Figure 9.1.
/// </summary>
public record Conversation(
    string Name,
    List<Participant> ParticipantRefs = null!,
    List<MessageFlow> MessageFlowRef = null!,
    List<CorrelationKey> CorrelationKeys = null!
) : ConversationNode(Name, ParticipantRefs, MessageFlowRef, CorrelationKeys);