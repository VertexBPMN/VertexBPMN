using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Lane, as per Figure 10.126.
/// </summary>
public record Lane(
    string Name,
    List<FlowNode> FlowNodeRefs = null!,
    LaneSet? ChildLaneSet = null,
    PartitionElement? PartitionElement = null,
    FlowNode? PartitionElementRef = null
) : BaseElement;