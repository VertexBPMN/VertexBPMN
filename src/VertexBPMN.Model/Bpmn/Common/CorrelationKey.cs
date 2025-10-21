using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Correlation key, as per Figure 8.17.
/// </summary>
public record CorrelationKey(
    string? Name = null,
    List<CorrelationProperty> CorrelationProperty = null!
) : BaseElement();