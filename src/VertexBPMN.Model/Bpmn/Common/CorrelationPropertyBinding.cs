using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Correlation property binding, as per Figure 8.17.
/// </summary>
public record CorrelationPropertyBinding(
    CorrelationProperty CorrelationPropertyRef,
    FormalExpression DataPath
) : BaseElement();