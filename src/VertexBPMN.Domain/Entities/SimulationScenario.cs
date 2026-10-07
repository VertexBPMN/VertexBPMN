namespace VertexBPMN.Domain.Entities;

public class SimulationScenario
{
	public string? BpmnXml { get; set; }
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string ProcessDefinitionId { get; set; } = string.Empty;
	[System.ComponentModel.DataAnnotations.Schema.NotMapped]
	public Dictionary<string, object> Variables { get; set; } = new(StringComparer.Ordinal);
	public int? MaxSteps { get; set; }
	public string TenantId { get; set; } = string.Empty;
}
