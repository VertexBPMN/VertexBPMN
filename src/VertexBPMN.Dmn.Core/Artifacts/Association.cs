using VertexBPMN.Domain.Model.Dmn.Core;

namespace VertexBPMN.Domain.Model.Dmn.Artifacts;

/// <summary>Association (6.3.6.1)</summary>
public sealed class Association : Artifact
{
    public AssociationDirection AssociationDirection { get; set; } = AssociationDirection.None;
    public DMNElement? SourceRef { get; set; }
    public DMNElement? TargetRef { get; set; }
}