namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class ContextEntry
{
    public string Name { get; set; } = string.Empty;
    public Expression? Value { get; set; }
}