using VertexBPMN.Domain.Model.Bpmn.Common.Faults;

namespace VertexBPMN.Domain.Model.Bpmn.Events;

public class EscalationEventDefinition : EventDefinition
{
    public Escalation? EscalationRef { get; set; }
}