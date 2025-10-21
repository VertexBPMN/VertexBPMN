using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Conditional event definition, as per Figure 10.78.
/// </summary>
public record ConditionalEventDefinition(
    Expression Condition
) : EventDefinition;