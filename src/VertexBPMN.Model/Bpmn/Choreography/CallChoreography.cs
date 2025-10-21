using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Collaboration;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Enums;

namespace VertexBPMN.Domain.Model.Bpmn.Choreography;

#nullable enable

/// <summary>
/// Call choreography, as per Figure 11.27.
/// </summary>
public record CallChoreography(
    Participant InitiatingParticipantRef,
    List<Participant> ParticipantRefs,
    List<CorrelationKey> CorrelationKeys,
    ChoreographyLoopType LoopType,
    Choreography CalledChoreographyRef,
    List<ParticipantAssociation> ParticipantAssociations = null!
) : ChoreographyActivity(InitiatingParticipantRef, ParticipantRefs, CorrelationKeys, LoopType);