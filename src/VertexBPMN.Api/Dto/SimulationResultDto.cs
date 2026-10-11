namespace VertexBPMN.Api.Dto;
internal class SimulationResultDto
{
	public string ProcessDefinitionId { get; set; } = string.Empty;
	public string TenantId { get; set; } = string.Empty;
	public List<SimulationStepDto> Steps { get; set; } = new();
	public bool Completed { get; set; }
	public string Message { get; set; } = string.Empty;
}
