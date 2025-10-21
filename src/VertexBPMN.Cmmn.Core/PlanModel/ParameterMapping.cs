using VertexBPMN.Domain.Model.Cmmn.Common;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class ParameterMapping : CmmnElement
{
    public Parameter SourceRef { get; set; }
    public Parameter TargetRef { get; set; }
    public Expression? Transformation { get; set; }
    public ParameterMapping(Parameter sourceRef, Parameter targetRef, Expression? transformation = null)
    { SourceRef = sourceRef; TargetRef = targetRef; Transformation = transformation; }
}