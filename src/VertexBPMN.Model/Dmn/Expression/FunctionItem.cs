using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.Expression;

public sealed class FunctionItem
{
    public string? OutputTypeRef { get; set; }
    public List<InformationItem> Parameters { get; } = new();
}