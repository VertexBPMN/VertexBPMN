using VertexBPMN.Domain.Model.Bpmn.Common.Expressions;
using VertexBPMN.Domain.Model.Bpmn.Common.Messages;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Correlation;

public class CorrelationPropertyRetrievalExpression : BaseElement
{
    public required Message MessageRef { get; set; }
    public required FormalExpression MessagePath { get; set; }
}