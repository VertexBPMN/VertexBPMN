namespace VertexBPMN.Domain.Model.Dmn.DI;

public sealed class DMNShape : DMNDiagramElement
{
    public Bounds Bounds { get; set; } = new();
    public DMNLabel? Label { get; set; }
}