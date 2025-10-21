using VertexBPMN.Domain.Model.Bpmn.Enums;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Association class, as per Figure 8.10.
/// </summary>
public record Association(
    BaseElement SourceRef,
    BaseElement TargetRef,
    AssociationDirection Direction = AssociationDirection.None
) : Artifact();