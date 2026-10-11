using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type SimulationStepAnalyticsDto retained for exposed HTTP action signatures and controller activation (SimulationStepAnalyticsDto).")]
public class SimulationStepAnalyticsDto
{
	public int StepNumber { get; set; }
	public string? ActivityId { get; set; }
	public string? ActivityName { get; set; }
	public Dictionary<string, object>? Variables { get; set; }
	public System.DateTime Timestamp { get; set; }
}
