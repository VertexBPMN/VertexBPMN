namespace VertexBPMN.Domain.Modeling;

/// <summary>
/// Handler for boundary events attached to activities
/// </summary>
public record BoundaryEventHandler(BpmnEvent BoundaryEvent);