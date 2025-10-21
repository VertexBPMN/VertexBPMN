namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// End event, as per Figure 10.69.
/// </summary>
public record EndEvent() : ThrowEvent;