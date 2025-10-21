using VertexBPMN.Domain.Model.Bpmn.Process;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Compensation event definition, as per Figure 10.76.
/// </summary>
public record CompensationEventDefinition(
    bool WaitForCompletion = true,
    Activity? ActivityRef = null
) : EventDefinition;