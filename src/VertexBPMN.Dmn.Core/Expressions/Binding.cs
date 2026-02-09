namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class Binding
{
    public InformationItem Parameter { get; set; } = new();
    public Expression? BindingFormula { get; set; }
}