namespace VertexBPMN.Api.Plugins;

public class ActivityExecutionResult
{
    public bool Success { get; set; }
    public Dictionary<string, object> OutputVariables { get; set; } = new();
    public string? Error { get; set; }
}