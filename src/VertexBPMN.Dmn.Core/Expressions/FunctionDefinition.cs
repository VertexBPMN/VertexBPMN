namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class FunctionDefinition : Expression
{
    public List<InformationItem> Parameters { get; } = new();
    public Expression? Body { get; set; }
    public string Kind { get; set; } = "FEEL";
}