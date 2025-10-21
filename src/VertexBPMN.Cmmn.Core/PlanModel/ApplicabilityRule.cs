using VertexBPMN.Domain.Model.Cmmn.Common;
using VertexBPMN.Domain.Model.Cmmn.Core;
using VertexBPMN.Domain.Model.Cmmn.InformationModel;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class ApplicabilityRule : CmmnElement
{
    public CaseFileItem? ContextRef { get; set; }
    public Expression Condition { get; set; }
    public ApplicabilityRule(Expression condition) => Condition = condition;
}