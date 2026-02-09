using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionTable;

public sealed class RuleAnnotationClause : DMNElement
{
    public string Name { get; set; } = string.Empty;
}