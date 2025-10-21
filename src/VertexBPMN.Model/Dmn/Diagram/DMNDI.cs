using VertexBPMN.Domain.Model.Dmn.DI;

namespace VertexBPMN.Domain.Model.Dmn.Diagram;

public sealed class DMNDI
{
    public List<DMNDiagram> Diagrams { get; } = new();
}