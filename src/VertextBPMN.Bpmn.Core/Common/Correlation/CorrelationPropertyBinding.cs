using VertexBPMN.Domain.Model.Bpmn.Common.Expressions;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Correlation;

public class CorrelationPropertyBinding : BaseElement
{
    public required CorrelationProperty CorrelationPropertyRef { get; set; }
    public required FormalExpression DataPath { get; set; }
}