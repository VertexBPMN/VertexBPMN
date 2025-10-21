using VertexBPMN.Domain.Model.Bpmn.Process;

namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Boundary event, as per Figure 10.69.
/// </summary>
public record BoundaryEvent(
    Activity AttachedToRef,
    bool CancelActivity = true
) : Event
{
    public List<EventDefinition> EventDefinitions { get; set; } = [];
}
