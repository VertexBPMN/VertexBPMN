using VertexBPMN.Domain.Model.Dmn.Expressions;

namespace VertexBPMN.Domain.Model.Dmn.DRD;

public abstract class Invocable : DRGElement
{
    public InformationItem Variable { get; set; } = new();
}