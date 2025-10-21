using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Escalation event definition, as per Figure 10.82.
/// </summary>
public record EscalationEventDefinition(
    Escalation? EscalationRef = null
) : EventDefinition;