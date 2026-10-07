namespace VertexBPMN.Application.Configuration;

public sealed class ServiceTaskDependencyOptions
{
	public bool Enabled { get; set; } = true;
	public List<string> Disabled { get; set; } = [];
	public Dictionary<string, string> Mappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
