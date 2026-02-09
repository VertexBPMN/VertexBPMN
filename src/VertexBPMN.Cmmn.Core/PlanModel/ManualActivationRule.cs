using VertexBPMN.Domain.Model.Cmmn.Common;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class ManualActivationRule : RuleBase { public ManualActivationRule(Expression c) : base(c) { }

    public ManualActivationRule()
    {
    }
}