namespace VertexBPMN.Domain.Model.Bpmn.Diagram;

/// <summary>
/// Bounds for shapes (external DC type).
/// </summary>
public record Bounds(
    double X,
    double Y,
    double Width,
    double Height
);