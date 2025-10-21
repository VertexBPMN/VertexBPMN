using VertexBPMN.Domain.Model.Cmmn.Core;

namespace VertexBPMN.Domain.Model.Cmmn.Artifacts;

public sealed class Association : CmmnElement
{
    public string? SourceRef { get; set; }
    public string? TargetRef { get; set; }
    public AssociationDirection Direction { get; set; } = AssociationDirection.None;
}