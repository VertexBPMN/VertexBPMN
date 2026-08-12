namespace VertexBPMN.Domain.Model.Dmn.DI;

public sealed class DMNEdge : DMNDiagramElement
{
    public List<Point> Waypoints { get; } = new();
    public DMNLabel? Label { get; set; }
}