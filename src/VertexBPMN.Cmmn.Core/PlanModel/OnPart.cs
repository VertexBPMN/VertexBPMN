using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public abstract class OnPart : CmmnElement
{
    public string? StandardEvent { get; set; }
}