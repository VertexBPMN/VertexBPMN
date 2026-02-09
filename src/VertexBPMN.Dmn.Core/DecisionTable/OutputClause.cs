using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.Expressions;

namespace VertexBPMN.Domain.Model.Dmn.DecisionTable;

public sealed class OutputClause : DMNElement
{
    public string? TypeRef { get; set; }
    public string? Name { get; set; }
    public UnaryTests? OutputValues { get; set; }
    public Expression? DefaultOutputEntry { get; set; }
}