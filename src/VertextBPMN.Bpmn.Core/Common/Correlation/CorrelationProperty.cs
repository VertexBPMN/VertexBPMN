using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Correlation;

public class CorrelationProperty : RootElement
{
    public string? Name { get; set; }
    public string? TypeRef { get; set; }
    public IReadOnlyList<CorrelationPropertyRetrievalExpression> RetrievalExpressions { get; } = [];
}