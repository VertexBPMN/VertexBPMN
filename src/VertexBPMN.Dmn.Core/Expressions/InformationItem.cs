using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class InformationItem : NamedElement
{
    public Expression? ValueExpression { get; set; }
    public string TypeRef { get; set; } = "Any";
}