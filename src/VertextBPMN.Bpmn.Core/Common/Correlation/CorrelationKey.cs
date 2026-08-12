using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Correlation;

public class CorrelationKey : BaseElement
{
    public string? Name { get; set; }
    public IReadOnlyList<CorrelationProperty> CorrelationPropertyRefs { get; } = [];
}