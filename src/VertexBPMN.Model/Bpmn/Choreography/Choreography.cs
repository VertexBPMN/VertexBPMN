using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Process;
using VertexBPMN.Domain.Model.Bpmn.Collaboration;

namespace VertexBPMN.Domain.Model.Bpmn.Choreography;

#nullable enable

/// <summary>
/// Choreography class, as per Figure 9.33.
/// </summary>
public record Choreography(
    List<Artifact> Artifacts = null!,
    List<LaneSet>? LaneSets = null,
    List<FlowElement>? FlowElements = null
) : Collaboration.Collaboration;