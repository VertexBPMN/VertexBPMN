namespace VertexBPMN.Domain.Model.Cmmn.DI;

public abstract class Shape : DiagramElement
{
    public Bounds Bounds { get; set; }
    protected Shape(Bounds bounds) => Bounds = bounds;
}