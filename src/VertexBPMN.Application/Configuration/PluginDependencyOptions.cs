namespace VertexBPMN.Application.Configuration;

public sealed class PluginDependencyOptions
{
	public bool Enabled { get; set; } = true;
	public string Directory { get; set; } = "plugins";
	public List<string> Files { get; set; } = [];
	public Dictionary<string, string> Sha256 { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
