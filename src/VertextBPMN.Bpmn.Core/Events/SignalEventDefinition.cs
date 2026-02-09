namespace VertexBPMN.Domain.Model.Bpmn.Events;

public class SignalEventDefinition : EventDefinition
{
    public Signal? SignalRef { get; set; }
}