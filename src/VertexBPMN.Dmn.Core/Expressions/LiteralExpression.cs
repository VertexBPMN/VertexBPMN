namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class LiteralExpression : Expression
{
    public string? Text { get; set; }
    public Uri? ExpressionLanguage { get; set; }
    public ImportedValues? ImportedValues { get; set; }
}