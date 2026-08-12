using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Common;

namespace VertexBPMN.Domain.Model.Cmmn.PlanModel;

public sealed class ProcessTask : Task
{
    public Process? ProcessRef { get; set; }
    public Expression? ProcessRefExpression { get; set; }
    public Collection<ParameterMapping> Mappings { get; } = new();
}