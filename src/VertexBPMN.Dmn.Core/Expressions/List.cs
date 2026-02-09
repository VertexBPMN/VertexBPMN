namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class List : Expression
{
    public System.Collections.Generic.List<Expression> Items { get; } = new();
}