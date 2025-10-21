using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DI;

public abstract class DMNDiagramElement
{
    public DMNElement? DmnElementRef { get; set; }
    public DMNStyle? Style { get; set; }
}