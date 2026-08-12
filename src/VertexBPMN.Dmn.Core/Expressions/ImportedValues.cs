using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.Expressions;

public sealed class ImportedValues
{
    public Import Import { get; set; } = new();
    public Uri? SelectionLanguage { get; set; }
    public string? Selection { get; set; }
}