namespace VertexBPMN.Api.Controllers;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type SimulationComparisonDto retained for exposed HTTP action signatures and controller activation (SimulationComparisonDto).")]
public class SimulationComparisonDto
{
	public int StepCountDifference { get; set; }
	public Dictionary<string, (object? A, object? B)>? VariableDifferences { get; set; }
	public bool CompletedDifference { get; set; }
}
