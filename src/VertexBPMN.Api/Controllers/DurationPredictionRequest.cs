using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using System.Security.Claims;
using VertexBPMN.Domain.Entities.ML;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type DurationPredictionRequest retained for exposed HTTP action signatures and controller activation (DurationPredictionRequest).")]
public class DurationPredictionRequest
{
	public string ProcessDefinitionKey { get; set; } = string.Empty;
	public Dictionary<string, object> Variables { get; set; } = new(StringComparer.Ordinal);
	public string? TenantId { get; set; }
}
