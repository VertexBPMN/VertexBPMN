namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class Invocation : Expression
{
    public Expression CalledFunction { get; set; } = default!;
    public List<Binding> Bindings { get; } = new();
}