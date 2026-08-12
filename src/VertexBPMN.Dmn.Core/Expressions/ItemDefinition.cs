using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class ItemDefinition : NamedElement
{
    public string TypeRef { get; set; } = "Any";
    public string? TypeLanguage { get; set; }
#pragma warning disable CS0618
    [Obsolete("Use TypeConstraint in DMN 1.5")]
    public UnaryTests? AllowedValues { get; set; }
#pragma warning restore CS0618
    public List<ItemDefinition> ItemComponents { get; } = new();
    public bool IsCollection { get; set; }
    public FunctionItem? FunctionItem { get; set; }
    public UnaryTests? TypeConstraint { get; set; }
}