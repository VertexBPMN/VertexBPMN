namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Intermediate throw event, as per Figure 10.69.
/// </summary>
public record IntermediateThrowEvent() : ThrowEvent;