using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Sub process, as per Figure 10.29.
/// </summary>
public record SubProcess(
    bool TriggeredByEvent = false,
    List<Artifact> Artifacts = null!,
    List<LaneSet>? LaneSets = null,
    List<FlowElement>? FlowElements = null,
    bool IsTransaction = false,
    bool IsMultiInstance = false
) : Activity
{
}