namespace VertexBPMN.Domain.Model.Bpmn.Event;

#nullable enable

/// <summary>
/// Implicit throw event, as per Figure 10.69.
/// </summary>
public record ImplicitThrowEvent() : ThrowEvent;