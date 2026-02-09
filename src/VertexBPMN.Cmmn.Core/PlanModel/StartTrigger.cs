using VertexBPMN.Domain.Model.Cmmn.Common;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public class StartTrigger : CmmnElement
{
    public Expression TimerExpression { get; internal set; }
}