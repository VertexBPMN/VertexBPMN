using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common.Flow;

namespace VertexBPMN.Domain.Model.Bpmn.Activities;

public class SubProcess : Activity
{
    public bool? TriggeredByEvent { get; set; }
    public IReadOnlyList<FlowElement> FlowElements { get; } = [];
}