namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class Relation : Expression
{
    public System.Collections.Generic.List<string> Columns { get; } = new();
    public System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, Expression?>> Rows { get; } = new();
}