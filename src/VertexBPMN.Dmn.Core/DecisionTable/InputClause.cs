using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.Expressions;

namespace VertexBPMN.Domain.Model.Dmn.DecisionTable;

public sealed class InputClause : DMNElement
{
    public Expression? InputExpression { get; set; }
    public UnaryTests? InputValues { get; set; }
}