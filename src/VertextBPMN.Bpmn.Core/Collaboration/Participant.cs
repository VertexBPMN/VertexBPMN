using VertexBPMN.Domain.Model.Bpmn.Foundation;
using VertexBPMN.Domain.Model.Bpmn.Processes;

namespace VertexBPMN.Domain.Model.Bpmn.Collaboration;

public class Participant : BaseElement
{
    public string? Name { get; set; }
    public Process? ProcessRef { get; set; }
    public ParticipantMultiplicity? Multiplicity { get; set; }
}