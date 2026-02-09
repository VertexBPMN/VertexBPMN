using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public abstract class Parameter : CmmnElement
{
    public string Name { get; set; } = string.Empty;
}