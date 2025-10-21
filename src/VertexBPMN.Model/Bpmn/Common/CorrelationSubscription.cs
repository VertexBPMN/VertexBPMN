using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Correlation subscription, as per Figure 8.17.
/// </summary>
public record CorrelationSubscription(
    CorrelationKey CorrelationKeyRef,
    List<CorrelationPropertyBinding> CorrelationPropertyBinding = null!
) : BaseElement();