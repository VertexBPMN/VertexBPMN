using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Process;

namespace VertexBPMN.Domain.Model.Bpmn.Choreography;

#nullable enable

/// <summary>
/// Global choreography task, as per the specification.
/// </summary>
public record GlobalChoreographyTask(
    List<Artifact> Artifacts = null!,
    List<LaneSet>? LaneSets = null,
    List<FlowElement>? FlowElements = null
) : Choreography(Artifacts, LaneSets, FlowElements);