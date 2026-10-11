namespace VertexBPMN.Api.Controllers;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type VariableTraceDto retained for exposed HTTP action signatures and controller activation (VariableTraceDto).")]
public class VariableTraceDto
{
	public int StepNumber { get; set; }
	public string? ActivityId { get; set; }
	public object? Value { get; set; }
}
