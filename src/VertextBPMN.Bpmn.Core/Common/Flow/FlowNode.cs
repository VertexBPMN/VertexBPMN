using System.Collections.Generic;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Flow;

public abstract class FlowNode : FlowElement
{
    public IReadOnlyList<SequenceFlow> Incoming { get; } = [];
    public IReadOnlyList<SequenceFlow> Outgoing { get; } = [];
}