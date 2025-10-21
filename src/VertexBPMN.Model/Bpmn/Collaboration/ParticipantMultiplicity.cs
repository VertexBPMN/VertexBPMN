using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

#nullable enable

/// <summary>
/// Participant multiplicity, as per Figure 9.9.
/// </summary>
public record ParticipantMultiplicity(
    int Minimum = 0,
    int Maximum = 1
) : BaseElement;