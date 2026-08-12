namespace VertexBPMN.Domain.Model.Dmn.DecisionTable;

using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class DecisionTable : Expression
{
    public List<InputClause> Inputs { get; } = new();
    public List<OutputClause> Outputs { get; } = new();
    public List<RuleAnnotationClause> Annotations { get; } = new();
    public List<DecisionRule> Rules { get; } = new();

    public HitPolicy HitPolicy { get; set; } = HitPolicy.UNIQUE;
    public BuiltinAggregator? Aggregation { get; set; }
    public DecisionTableOrientation? PreferredOrientation { get; set; }
    public string? OutputLabel { get; set; }
}