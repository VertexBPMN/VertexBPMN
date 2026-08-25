using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
[ApiController]
[Route("api/vertex/signal")]
[Authorize(Policy = "ProcessManager")]
public class VertexSignalController : ControllerBase
{
    private readonly IRuntimeService _runtimeService;

    public VertexSignalController(IRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    [HttpPost]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastSignalRequest request)
    {
        var tenantId = ResolveTenantId(request.TenantId);
        if (tenantId is null && !User.IsInRole("Admin")) return Forbid();
        await _runtimeService.BroadcastSignalAsync(
            request.SignalName,
            request.Variables,
            tenantId: tenantId,
            idempotencyKey: Request.Headers["Idempotency-Key"].FirstOrDefault());
        return Ok();
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");

    public record BroadcastSignalRequest(string SignalName, IDictionary<string, object>? Variables, string? TenantId = null);
}
