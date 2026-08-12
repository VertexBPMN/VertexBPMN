using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.InformationModel;

public sealed class CaseFileItem : CmmnElement
{
    public CaseFileItemDefinition? Definition { get; set; }
    public Collection<CaseFileItem> Children { get; } = new();
    public CaseFileItem? Parent { get; set; }
    public Dictionary<string, object?> Properties { get; } = new();
}
