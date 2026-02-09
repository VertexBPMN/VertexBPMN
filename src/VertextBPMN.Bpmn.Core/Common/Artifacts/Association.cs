using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Artifacts;

public class Association : Artifact
{
    public AssociationDirection AssociationDirection { get; set; } = AssociationDirection.None;
    public BaseElement? SourceRef { get; set; }
    public BaseElement? TargetRef { get; set; }
}