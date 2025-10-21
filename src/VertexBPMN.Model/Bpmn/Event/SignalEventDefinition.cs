using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Signal event definition, as per Figure 10.93.
/// </summary>
public record SignalEventDefinition(
    Signal? SignalRef = null
) : EventDefinition;