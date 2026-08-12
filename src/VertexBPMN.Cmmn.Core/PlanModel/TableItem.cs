using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public abstract class TableItem : CmmnElement
{
    public ApplicabilityRule? ApplicabilityRule { get; set; }
}