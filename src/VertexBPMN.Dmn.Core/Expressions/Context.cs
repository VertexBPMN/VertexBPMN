namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class Context : Expression
{
    public List<ContextEntry> Entries { get; } = new();
    public Expression? Result { get; set; }
}