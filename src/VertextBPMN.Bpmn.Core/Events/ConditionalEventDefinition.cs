using VertexBPMN.Domain.Model.Bpmn.Common.Expressions;

namespace VertexBPMN.Domain.Model.Bpmn.Events;

public class ConditionalEventDefinition : EventDefinition
{
    public Expression? Condition { get; set; }
}