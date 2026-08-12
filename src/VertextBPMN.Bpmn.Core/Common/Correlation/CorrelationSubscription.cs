using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Correlation;

public class CorrelationSubscription : BaseElement
{
    public string? ProcessRef { get; set; }
    public required CorrelationKey CorrelationKeyRef { get; set; }
    public IReadOnlyList<CorrelationPropertyBinding> CorrelationPropertyBindings { get; } = [];
}