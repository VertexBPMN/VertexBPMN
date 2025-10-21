namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class Conditional : Expression
{
    public Expression If { get; set; } = default!;
    public Expression Then { get; set; } = default!;
    public Expression Else { get; set; } = default!;
}