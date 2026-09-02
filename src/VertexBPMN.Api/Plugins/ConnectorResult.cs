namespace VertexBPMN.Api.Plugins;

public class ConnectorResult
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
}