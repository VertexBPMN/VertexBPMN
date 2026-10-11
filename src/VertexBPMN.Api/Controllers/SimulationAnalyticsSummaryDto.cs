using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type SimulationAnalyticsSummaryDto retained for exposed HTTP action signatures and controller activation (SimulationAnalyticsSummaryDto).")]
public class SimulationAnalyticsSummaryDto
{
	public string? ProcessDefinitionId { get; set; }
	public string? TenantId { get; set; }
	public int StepCount { get; set; }
	public bool Completed { get; set; }
	public Dictionary<string, object>? Variables { get; set; }
}
