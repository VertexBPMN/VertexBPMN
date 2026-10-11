namespace VertexBPMN.Api.Dto;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type SimulationScenarioDto retained for exposed HTTP action signatures and controller activation (SimulationScenarioDto).")]
public class SimulationScenarioDto
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string ProcessDefinitionId { get; set; } = string.Empty;
	public Dictionary<string, object> Variables { get; set; } = new(StringComparer.Ordinal);
	public int? MaxSteps { get; set; }
	public string TenantId { get; set; } = string.Empty;
}
