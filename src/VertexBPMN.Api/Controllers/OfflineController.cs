using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/offline/completions")]
[Authorize]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public class OfflineController : ControllerBase
{
	private readonly IOfflineCompletionReplayService _replay;
	public OfflineController(IOfflineCompletionReplayService replay)
	{
		_replay = replay;
	}

	/// <summary>
	/// Replays a client's offline task completions after reconnect. Each item is applied at most
	/// once via the idempotent, revision/schema-guarded completion contract; results surface
	/// Completed / Conflict / NotFound / Forbidden per item so the client can show conflicts
	/// instead of silently overwriting newer server state (LC11 slice 3).
	/// </summary>
	[HttpPost("replay")]
	[Authorize(Policy = "ProcessManager")]
	[ProducesResponseType(200)]
	public async Task<IActionResult> Replay([FromBody] OfflineReplayRequest request, CancellationToken ct)
	{
		var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
		var results = await _replay.ReplayAsync(
			request.Items,
			User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier),
			ResolveTenantId(request.TenantId),
			User.IsInRole("Admin"),
			roles,
			ct);
		return Ok(new OfflineReplayResponse(results));
	}

	private string? ResolveTenantId(string? requestedTenantId)
	{
		if (!User.IsInRole("Admin"))
		{
			return User.FindFirstValue("tenant_id");
		}

		return string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed HTTP action signature and controller activation (OfflineController).")]
	public record OfflineReplayRequest(IReadOnlyList<OfflineCompletionCommand> Items, string? TenantId = null);
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed HTTP action signature and controller activation (OfflineController).")]
	public record OfflineReplayResponse(IReadOnlyList<OfflineCompletionReplayResult> Results);
}
