using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Error event definition, as per Figure 10.80.
/// </summary>
public record ErrorEventDefinition(
    Error? ErrorRef = null
) : EventDefinition;