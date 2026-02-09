using VertexBPMN.Domain.Model.Bpmn.Common.Expressions;

namespace VertexBPMN.Domain.Model.Bpmn.Common.Flow;

public class SequenceFlow : FlowElement
{
    public FlowNode? SourceRef { get; set; }
    public FlowNode? TargetRef { get; set; }
    public Expression? ConditionExpression { get; set; }
    public bool? IsImmediate { get; set; }
}