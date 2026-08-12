using VertexBPMN.Domain.Model.Cmmn.Common;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class IfPart : CmmnElement
{
    public Expression Condition { get; set; }
    public IfPart(Expression condition) => Condition = condition;

    public IfPart()
    {
    }
}