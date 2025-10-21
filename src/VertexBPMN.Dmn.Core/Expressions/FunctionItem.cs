namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class FunctionItem
{
    public string? OutputTypeRef { get; set; }
    public List<InformationItem> Parameters { get; } = new();
}