using VertexBPMN.Domain.Model.Dmn.Expressions;

namespace VertexBPMN.Domain.Model.Dmn.DRD;

public sealed class InputData : DRGElement
{
    public InformationItem Variable { get; set; } = new();
}