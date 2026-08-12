namespace VertexBPMN.Domain.Model.Bpmn.Events;

public class CompensationEventDefinition : EventDefinition
{
    public Bpmn.Activities.Activity? ActivityRef { get; set; }
}