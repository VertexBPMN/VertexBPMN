namespace VertexBPMN.Domain.Model.Dmn.DI;

public sealed class DMNDiagram
{
    public string? Name { get; set; }
    public List<DMNDiagramElement> Elements { get; } = new();
}