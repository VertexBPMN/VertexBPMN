using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Data association, as per Figure 10.64.
/// </summary>
public record DataAssociation(
    List<ItemAwareElement> SourceRef,
    ItemAwareElement TargetRef,
    List<FormalExpression>? Transformation = null,
    List<Assignment>? Assignment = null
) : BaseElement();