using VertexBPMN.Domain.Model.Cmmn.Common;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class TimerStartTrigger : StartTrigger
{
    public Expression? TimerExpression { get; set; }
}