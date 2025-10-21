namespace VertexBPMN.Domain.Model.Bpmn.Activities;

public class CallActivity : Activity
{
    public required string CalledElement { get; set; }
}