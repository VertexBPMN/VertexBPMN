using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Collaboration;
using VertexBPMN.Domain.Model.Bpmn.Enums;
using VertexBPMN.Domain.Model.Bpmn.Process;

namespace VertexBPMN.Domain.Model.Bpmn.Choreography;

#nullable enable

/// <summary>
/// Sub choreography, as per the specification.
/// </summary>
public record SubChoreography(
    Participant InitiatingParticipantRef,
    List<Participant> ParticipantRefs = null!,
    List<CorrelationKey> CorrelationKeys = null!,
    ChoreographyLoopType LoopType = ChoreographyLoopType.None,
    List<Artifact> Artifacts = null!,
    List<LaneSet>? LaneSets = null,
    List<FlowElement>? FlowElements = null
) : ChoreographyActivity(InitiatingParticipantRef, ParticipantRefs, CorrelationKeys, LoopType);