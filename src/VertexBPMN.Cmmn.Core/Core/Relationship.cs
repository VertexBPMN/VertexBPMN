using System.Collections.ObjectModel;

namespace VertexBPMN.Domain.Model.Cmmn.Core;

public sealed class Relationship : CmmnElement
{
    public Collection<string> Sources { get; } = new();
    public Collection<string> Targets { get; } = new();
    public string? Type { get; set; }
}
