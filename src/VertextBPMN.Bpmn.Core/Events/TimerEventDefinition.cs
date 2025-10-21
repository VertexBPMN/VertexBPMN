using VertexBPMN.Domain.Model.Bpmn.Common.Expressions;

namespace VertexBPMN.Domain.Model.Bpmn.Events;

public class TimerEventDefinition : EventDefinition
{
    public Expression? TimeDate { get; set; }
    public Expression? TimeDuration { get; set; }
    public Expression? TimeCycle { get; set; }
}