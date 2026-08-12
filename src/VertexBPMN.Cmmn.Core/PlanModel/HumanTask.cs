using VertexBPMN.Domain.Model.Cmmn.CaseModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class HumanTask : Task
{
    public PlanningTable? PlanningTable { get; set; }
    public Role? PerformerRef { get; set; }
}