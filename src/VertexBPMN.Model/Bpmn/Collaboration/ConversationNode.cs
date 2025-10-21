using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

#nullable enable

/// <summary>
/// Abstract conversation node, as per Figure 9.1.
/// </summary>
public abstract record ConversationNode(
    string Name,
    List<Participant> ParticipantRefs = null!,
    List<MessageFlow> MessageFlowRef = null!,
    List<CorrelationKey> CorrelationKeys = null!
) : InteractionNode;