using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public abstract class PlanItemDefinition : CmmnElement
{
    public PlanItemControl? DefaultControl { get; set; }
}