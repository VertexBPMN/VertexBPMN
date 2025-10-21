using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Message event definition, as per Figure 10.89.
/// </summary>
public record MessageEventDefinition(
    Message? MessageRef = null,
    Operation? OperationRef = null
) : EventDefinition
{
    public string CorrelationKey { get; internal set; }
}