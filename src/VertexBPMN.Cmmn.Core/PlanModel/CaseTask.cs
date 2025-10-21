using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.CaseModel;
using VertexBPMN.Domain.Model.Cmmn.Common;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class CaseTask : Task
{
    public Case? CaseRef { get; set; }
    public Expression? CaseRefExpression { get; set; }
    public Collection<ParameterMapping> Mappings { get; } = new();
}