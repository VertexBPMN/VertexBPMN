using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

public class ParticipantAssociation : BaseElement
{
    public Participant? InnerParticipantRef { get; set; }
    public Participant? OuterParticipantRef { get; set; }
}