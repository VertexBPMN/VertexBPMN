using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common.Flow;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Processes;

public class Lane : BaseElement
{
    public string? Name { get; set; }
    public IReadOnlyList<FlowNode> FlowNodeRefs { get; } = [];
    public LaneSet? ChildLaneSet { get; set; }
}