using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Correlation property retrieval expression, as per Figure 8.17.
/// </summary>
public record CorrelationPropertyRetrievalExpression(
    FormalExpression MessagePath,
    Message MessageRef
) : BaseElement();