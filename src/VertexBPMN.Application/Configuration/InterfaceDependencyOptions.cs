namespace VertexBPMN.Application.Configuration;

public sealed class InterfaceDependencyOptions
{
	public bool AiDecisionService { get; set; } = true;
	public bool McpAgentService { get; set; } = true;
	public bool LoadBalancing { get; set; } = true;
}
