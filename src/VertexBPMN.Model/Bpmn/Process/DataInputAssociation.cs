using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Data input association, as per Figure 10.64.
/// </summary>
public record DataInputAssociation(
    List<ItemAwareElement> SourceRef,
    ItemAwareElement TargetRef,
    List<FormalExpression>? Transformation = null,
    List<Assignment>? Assignment = null
) : DataAssociation(SourceRef, TargetRef, Transformation, Assignment);