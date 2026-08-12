using System.Collections.ObjectModel;

namespace VertexBPMN.Domain.Model.Cmmn.DI;

public abstract class Edge : DiagramElement
{
    public Collection<Point> Waypoints { get; } = new();
}