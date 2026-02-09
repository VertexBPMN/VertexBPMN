using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Faults;

public class Escalation : RootElement
{
    public string? Name { get; set; }
    public string? EscalationCode { get; set; }
    public string? StructureRef { get; set; }
}