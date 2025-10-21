using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

#nullable enable

/// <summary>
/// Participant association, as per Figure 9.10.
/// </summary>
public record ParticipantAssociation(
    Participant InnerParticipantRef,
    Participant OuterParticipantRef
) : BaseElement;