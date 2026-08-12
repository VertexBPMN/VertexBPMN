namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class Filter : Expression
{
    public Expression Collection { get; set; } = default!;
    public Expression Predicate { get; set; } = default!;
}