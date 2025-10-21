namespace VertexBPMN.Domain.Model.Cmmn.Diagram;

/// <summary>
/// Bounds for shapes (external DC type).
/// </summary>
public record Bounds(
    double X,
    double Y,
    double Width,
    double Height
);