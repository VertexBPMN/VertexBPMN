namespace VertexBPMN.Application.Configuration;

public sealed class AiModelOptions
{
	public bool Enabled { get; set; } = true;
	public string Provider { get; set; } = string.Empty;
	public string Model { get; set; } = string.Empty;
	public string? Endpoint { get; set; }
	public string? ApiKeyEnvironmentVariable { get; set; }
}
