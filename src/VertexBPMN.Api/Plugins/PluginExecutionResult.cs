namespace VertexBPMN.Api.Plugins;

public class PluginExecutionResult
{
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
}