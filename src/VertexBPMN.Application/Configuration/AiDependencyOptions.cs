namespace VertexBPMN.Application.Configuration;

public sealed class AiDependencyOptions
{
	public bool Enabled { get; set; } = true;
	public string DefaultProvider { get; set; } = "openai";
	public string DefaultModel { get; set; } = "gpt-4";
	public Dictionary<string, AiModelOptions> Models { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
