using VertexBPMN.Domain.Model.Cmmn.Common;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public abstract class RuleBase : CmmnElement
{
    public Expression Condition { get; set; }
    protected RuleBase(Expression condition) => Condition = condition;
    protected RuleBase()
    {
            
    }
}