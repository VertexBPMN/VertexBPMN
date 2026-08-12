namespace VertexBPMN.Domain.Model.Bpmn.Choreography;

public class CallChoreography : ChoreographyActivity
{
    public Choreography? CalledChoreographyRef { get; set; }
}