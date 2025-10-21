using VertexBPMN.Domain.Model.Dmn.Enums;

namespace VertexBPMN.Domain.Model.Dmn.DecisionLogic;

#nullable enable

/// <summary>
/// DecisionTable (Figure 8-20, extends Expression). 
/// Notation: Jagged box for inputs/outputs/rules. 
/// Semantics: Evaluates rules per hit policy; outputs aggregated.
/// DMN 1.5: OutputOrder hit policy; builtins handle nulls.
/// </summary>
public record DecisionTable(
    List<InputClause> Inputs = null!,
    List<OutputClause> Outputs = null!,
    List<RuleAnnotationClause> Annotations = null!,
    List<DecisionRule> Rules = null!,
    HitPolicy HitPolicy = HitPolicy.Unique,
    BuiltinAggregator? Aggregation = null,
    DecisionTableOrientation PreferredOrientation = DecisionTableOrientation.RuleAsRow,
    string? OutputLabel = null
) : Core.Expression();