using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Collaboration;
using VertexBPMN.Domain.Model.Bpmn.Common.Flow;

namespace VertexBPMN.Domain.Model.Bpmn.Choreography;

public class Choreography : FlowElementsContainer
{
    public IReadOnlyList<FlowElement> FlowElements { get; } = [];
    public IReadOnlyList<Participant> Participants { get; } = [];
}