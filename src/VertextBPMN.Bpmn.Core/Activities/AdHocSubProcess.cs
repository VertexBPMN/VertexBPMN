namespace VertexBPMN.Domain.Model.Bpmn.Activities;

public class AdHocSubProcess : SubProcess
{
    public bool? CancelRemainingInstances { get; set; }
    public string? Ordering { get; set; }
}