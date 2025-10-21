using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionLogic;

/// <summary>
/// DecisionRule for decision table.
/// </summary>
public record DecisionRule(
    List<UnaryTests> InputEntries = null!,
    List<LiteralExpression> OutputEntries = null!,
    List<RuleAnnotation> AnnotationEntries = null!
) : DMNElement();