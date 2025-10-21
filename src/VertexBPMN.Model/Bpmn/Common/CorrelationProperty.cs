using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Correlation property, as per Figure 8.17.
/// </summary>
public record CorrelationProperty(
    string? Name = null,
    List<CorrelationPropertyRetrievalExpression> CorrelationPropertyRetrievalExpression = null!,
    string? Type = null
) : RootElement();