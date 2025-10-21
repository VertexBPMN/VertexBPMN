namespace VertexBPMN.Domain.Model.Bpmn.Events;

public class StartEvent : CatchEvent
{
    public bool? IsInterrupting { get; set; }
}