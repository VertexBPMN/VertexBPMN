namespace VertexBPMN.Domain.Model.Bpmn.Common.Expressions;

public class FormalExpression : Expression
{
    public string? Language { get; set; }
    public string? EvaluatesToTypeRef { get; set; }
}