namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class UserEventStartTrigger : StartTrigger
{
    public UserEventListener? SourceRef { get; set; }
}