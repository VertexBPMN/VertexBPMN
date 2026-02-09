using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.DecisionTable;

public sealed class RuleAnnotation : DMNElement
{
    public string? Text { get; set; }
}