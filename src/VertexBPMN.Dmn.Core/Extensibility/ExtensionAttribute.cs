namespace VertexBPMN.Domain.Model.Dmn.Extensibility;

public sealed class ExtensionAttribute
{
    public string Name { get; init; } = string.Empty;
    public object? Value { get; init; }
    public object? ValueRef { get; init; }
}