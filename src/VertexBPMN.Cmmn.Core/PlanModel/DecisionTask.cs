using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Common;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class DecisionTask : Task
{
    public Decision? DecisionRef { get; set; }
    public Expression? DecisionRefExpression { get; set; }
    public Collection<ParameterMapping> Mappings { get; } = new();
}