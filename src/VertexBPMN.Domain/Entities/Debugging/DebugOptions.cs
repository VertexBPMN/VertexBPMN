namespace VertexBPMN.Domain.Entities.Debugging;

public class DebugOptions
{
    public bool PauseOnStart { get; set; } = false;
    public bool PauseOnError { get; set; } = true;
    public bool RecordVariableChanges { get; set; } = true;
    public bool EnablePerformanceMetrics { get; set; } = true;
    public List<string> WatchedVariables { get; set; } = new();
}