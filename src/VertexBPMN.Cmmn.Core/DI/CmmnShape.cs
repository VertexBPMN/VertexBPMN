namespace VertexBPMN.Domain.Model.Cmmn.DI;

public sealed class CmmnShape : CmmnDiagramElement
{
    public Bounds Bounds { get; set; }
    public bool? IsCollapsed { get; set; }
    public bool? IsPlanningTableCollapsed { get; set; }
    public CmmnShape(Bounds bounds) => Bounds = bounds;

    public CmmnShape()
    {
    }
}