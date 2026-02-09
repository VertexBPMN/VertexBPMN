using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common.Flow;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

public class Collaboration :  FlowElementsContainer
{
    public IReadOnlyList<FlowElement> FlowElements { get; } = [];
    public IReadOnlyList<Participant> Participants { get; } = [];
    public IReadOnlyList<MessageFlow> MessageFlows { get; } = [];
}