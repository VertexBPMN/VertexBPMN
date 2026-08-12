using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

public class ParticipantMultiplicity : BaseElement
{
    public int? Minimum { get; set; }
    public int? Maximum { get; set; }
}