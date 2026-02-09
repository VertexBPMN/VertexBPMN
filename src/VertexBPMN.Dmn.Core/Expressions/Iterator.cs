namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class Iterator : Expression
{
    public string Variable { get; set; } = "x";
    public Expression In { get; set; } = default!;
    public Expression Return { get; set; } = default!;
    public string Kind { get; set; } = "for";
}