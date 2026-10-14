using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VertexBPMN.Application.Connectors;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/mapping")]
[Authorize(Policy = "ProcessManager")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public sealed class MappingController : ControllerBase
{
	/// <summary>Stateless Studio mapper preview: parses sample source JSON and applies explicit
	/// field mappings, returning the resulting target values (LC03 Z.247).</summary>
	[HttpPost("preview")]
	[ProducesResponseType(typeof(FieldMappingResult), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public IActionResult Preview([FromBody] MappingPreviewRequest request)
	{
		if (!ResolveTenant(out var _))
		{
			return Forbid();
		}

		var mappings = new List<FieldMapping>(request.Mappings.Count);
		foreach (var item in request.Mappings)
		{
			var transform = FieldTransform.Copy;
			if (!string.IsNullOrWhiteSpace(item.Transform)
				&& !Enum.TryParse(item.Transform, ignoreCase: true, out transform))
			{
				return BadRequest(new ProblemDetails
				{
					Title = $"Unsupported transform '{item.Transform}'.",
					Detail = $"Supported transforms: {string.Join(", ", Enum.GetNames<FieldTransform>())}."
				});
			}

			mappings.Add(new FieldMapping(item.SourcePath, item.TargetKey, transform, item.Required));
		}

		return Ok(FieldMappingPreviewService.Apply(request.SourceJson, mappings));
	}

	private bool ResolveTenant(out string? tenant)
	{
		tenant = null;
		if (User.IsInRole("Admin"))
		{
			tenant = "default";
			return true;
		}

		tenant = User.FindFirstValue("tenant_id");
		return !string.IsNullOrWhiteSpace(tenant);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed HTTP action signatures (MappingController).")]
	public sealed record MappingPreviewItem(string SourcePath, string TargetKey, string? Transform, bool Required);

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed HTTP action signatures (MappingController).")]
	public sealed record MappingPreviewRequest(string? SourceJson, IReadOnlyList<MappingPreviewItem> Mappings);
}
