using VertexBPMN.Domain.Model.Cmmn.Common;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class RequiredRule : RuleBase 
{ 
    public RequiredRule(Expression c) : base(c) { }

    public RequiredRule() : base()
    {
    }
}