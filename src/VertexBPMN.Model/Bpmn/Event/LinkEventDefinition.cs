using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Link event definition, as per the specification.
/// </summary>
public record LinkEventDefinition(
    string Name,
    List<FlowElement> Source = null!,
    FlowElement? Target = null
) : EventDefinition;