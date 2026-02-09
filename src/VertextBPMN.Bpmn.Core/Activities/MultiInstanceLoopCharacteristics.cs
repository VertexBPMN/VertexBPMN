using VertexBPMN.Domain.Model.Bpmn.Common.Expressions;

namespace VertexBPMN.Domain.Model.Bpmn.Activities;

public class MultiInstanceLoopCharacteristics : LoopCharacteristics
{
    public bool IsSequential { get; set; }
    public Expression? LoopCardinality { get; set; }
    public Expression? CompletionCondition { get; set; }
    public Bpmn.Data.ItemAwareElement? LoopDataInputRef { get; set; }
    public Bpmn.Data.ItemAwareElement? LoopDataOutputRef { get; set; }
    public Bpmn.Data.DataInput? InputDataItem { get; set; }
    public Bpmn.Data.DataOutput? OutputDataItem { get; set; }
}