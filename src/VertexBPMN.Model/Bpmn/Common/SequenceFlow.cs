namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Sequence flow, as per Figure 8.35.
/// </summary>
public record SequenceFlow(
    FlowNode SourceRef,
    FlowNode TargetRef,
    Expression? ConditionExpression = null,
    bool IsImmediate = true
) : FlowElement();