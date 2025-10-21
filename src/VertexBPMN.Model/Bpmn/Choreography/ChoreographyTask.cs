using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Collaboration;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Enums;

namespace VertexBPMN.Domain.Model.Bpmn.Choreography;

#nullable enable

/// <summary>
/// Choreography task, as per Figure 11.27.
/// </summary>
public record ChoreographyTask(
    Participant InitiatingParticipantRef,
    List<Participant> ParticipantRefs = null!,
    List<CorrelationKey> CorrelationKeys = null!,
    ChoreographyLoopType LoopType = ChoreographyLoopType.None
) : ChoreographyActivity(InitiatingParticipantRef, ParticipantRefs, CorrelationKeys, LoopType);