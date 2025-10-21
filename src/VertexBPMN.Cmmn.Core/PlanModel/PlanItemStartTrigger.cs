namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class PlanItemStartTrigger : StartTrigger
{
    public PlanItem? SourceRef { get; set; }
    public string? StandardEvent { get; set; }
}