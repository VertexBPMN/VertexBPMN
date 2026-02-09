using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Collaboration;
using VertexBPMN.Domain.Model.Bpmn.Common.Flow;

namespace VertexBPMN.Domain.Model.Bpmn.Choreography;

public abstract class ChoreographyActivity : FlowNode
{
    public IReadOnlyList<Participant> Participants { get; } = [];
}