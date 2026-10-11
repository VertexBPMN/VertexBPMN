using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace VertexBPMN.Api.Controllers;
[ApiController]
[Route("api/vertex/signal")]
[Authorize(Policy = "ProcessManager")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public class VertexSignalController : ControllerBase
{
	[FromHeader(Name = "Idempotency-Key")]
	public string? IdempotencyKey { get; set; }

	private readonly IRuntimeService _runtimeService;
	public VertexSignalController(IRuntimeService runtimeService)
	{
		_runtimeService = runtimeService;
	}

	[HttpPost]
	public async Task<IActionResult> Broadcast([FromBody] BroadcastSignalRequest request)
	{
		var tenantId = ResolveTenantId(request.TenantId);
		if (tenantId is null && !User.IsInRole("Admin"))
		{
			return Forbid();
		}

		await _runtimeService.BroadcastSignalAsync(request.SignalName, request.Variables, tenantId: tenantId, idempotencyKey: IdempotencyKey);
		return Ok();
	}

	private string? ResolveTenantId(string? requestedTenantId)
	{
		if (!User.IsInRole("Admin"))
		{
			return User.FindFirstValue("tenant_id");
		}

		return string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type BroadcastSignalRequest retained for exposed HTTP action signatures and controller activation (VertexSignalController).")]
	public record BroadcastSignalRequest(string SignalName, IDictionary<string, object>? Variables, string? TenantId = null);
}
