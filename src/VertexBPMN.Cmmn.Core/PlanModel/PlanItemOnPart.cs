namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class PlanItemOnPart : OnPart
{
    public PlanItem? SourceRef { get; set; }
    public string? Transition { get; set; }
}