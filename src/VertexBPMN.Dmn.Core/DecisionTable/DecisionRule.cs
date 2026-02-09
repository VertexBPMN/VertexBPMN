using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.Expressions;

namespace VertexBPMN.Domain.Model.Dmn.DecisionTable;

public sealed class DecisionRule : DMNElement
{
    public List<UnaryTests> InputEntry { get; } = new();
    public List<LiteralExpression> OutputEntry { get; } = new();
    public List<RuleAnnotation> AnnotationEntry { get; } = new();
}