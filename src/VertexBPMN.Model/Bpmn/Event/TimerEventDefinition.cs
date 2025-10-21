using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Timer event definition, as per the specification.
/// </summary>
public record TimerEventDefinition(
    Expression? TimeDate = null,
    Expression? TimeDuration = null,
    Expression? TimeCycle = null
) : EventDefinition;