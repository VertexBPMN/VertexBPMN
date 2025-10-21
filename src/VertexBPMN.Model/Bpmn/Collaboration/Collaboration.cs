using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

#nullable enable

/// <summary>
/// Collaboration class, as per Figure 9.1.
/// </summary>
public record Collaboration(
    string? Name = null,
    bool IsClosed = false,
    List<Participant> Participants = null!,
    List<MessageFlow> MessageFlows = null!,
    List<Artifact> Artifacts = null!,
    List<ConversationNode> Conversations = null!,
    List<ConversationAssociation> ConversationAssociations = null!,
    List<ParticipantAssociation> ParticipantAssociations = null!,
    List<MessageFlowAssociation> MessageFlowAssociations = null!,
    List<CorrelationKey> CorrelationKeys = null!,
    List<Choreography.Choreography> ChoreographyRef = null!,
    List<ConversationLink> ConversationLinks = null!
) : RootElement;