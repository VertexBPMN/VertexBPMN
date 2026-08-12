using System.Collections.ObjectModel;

namespace VertexBPMN.Domain.Model.Cmmn.DI;

public sealed class CmmnEdge : CmmnDiagramElement
{
    public Collection<Point> Waypoints { get; } = new();
    public bool? IsStandardEventVisible { get; set; }
}