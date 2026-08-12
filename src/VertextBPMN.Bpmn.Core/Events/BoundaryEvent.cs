namespace VertexBPMN.Domain.Model.Bpmn.Events;

public class BoundaryEvent : CatchEvent
{
    public required Bpmn.Activities.Activity AttachedToRef { get; set; }
    public bool? CancelActivity { get; set; }
}