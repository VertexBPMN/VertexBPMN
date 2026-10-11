namespace VertexBPMN.Api.Plugins;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public plugin extension contract retained for implementations in external assemblies.")]
public class ActivityExecutionResult
{
    public bool Success { get; set; }
    public Dictionary<string, object> OutputVariables { get; set; } = new(StringComparer.Ordinal);
    public string? Error { get; set; }
}
