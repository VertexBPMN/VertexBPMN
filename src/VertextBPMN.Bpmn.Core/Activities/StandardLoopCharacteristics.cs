using VertexBPMN.Domain.Model.Bpmn.Common.Expressions;

namespace VertexBPMN.Domain.Model.Bpmn.Activities;

public class StandardLoopCharacteristics : LoopCharacteristics
{
    public Expression? LoopCondition { get; set; }
    public int? LoopMaximum { get; set; }
}