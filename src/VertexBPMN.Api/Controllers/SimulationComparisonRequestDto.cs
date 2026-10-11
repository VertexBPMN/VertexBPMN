using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Api.Controllers;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type SimulationComparisonRequestDto retained for exposed HTTP action signatures and controller activation (SimulationComparisonRequestDto).")]
public class SimulationComparisonRequestDto
{
	public SimulationResult? ResultA { get; set; }
	public SimulationResult? ResultB { get; set; }
}
