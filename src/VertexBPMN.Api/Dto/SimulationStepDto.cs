namespace VertexBPMN.Api.Dto;

internal class SimulationStepDto
{
	public int StepNumber { get; set; }
	public string ActivityId { get; set; } = string.Empty;
	public string ActivityName { get; set; } = string.Empty;
	public Dictionary<string, object> Variables { get; set; } = new(StringComparer.Ordinal);
	public DateTime Timestamp { get; set; }
}
