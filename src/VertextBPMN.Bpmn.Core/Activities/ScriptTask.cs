namespace VertexBPMN.Domain.Model.Bpmn.Activities;

public class ScriptTask : Task
{
    public string? Script { get; set; }
    public string? ScriptFormat { get; set; }
}