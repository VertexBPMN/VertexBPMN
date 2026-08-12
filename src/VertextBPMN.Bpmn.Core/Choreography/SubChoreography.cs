using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common.Flow;

namespace VertexBPMN.Domain.Model.Bpmn.Choreography;

public class SubChoreography : ChoreographyActivity
{
    public IReadOnlyList<FlowElement> FlowElements { get; } = [];
}