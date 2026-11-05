using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VertexBPMN.Portal.Bff.Controllers;

[ApiController]
[Route("api/bff/tasks")]
[Authorize]
public sealed class BffTaskController(IBffRuntimeGateway gateway) : ControllerBase
{
    [HttpGet("inbox")]
    public async Task<IActionResult> Inbox([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var tenant = User.FindFirstValue("tenant_id");
        return tenant is null ? Forbid() : Content(await gateway.GetInboxAsync(tenant, page, pageSize, ct), "application/json");
    }

    [HttpPost("inbox/{taskId:guid}/claim")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Claim(Guid taskId, CancellationToken ct = default) => Mutate(taskId, null, false, ct);

    [HttpPost("inbox/{taskId:guid}/complete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Complete(Guid taskId, [FromBody] BffApplicationController.CompleteRunRequest? request, CancellationToken ct = default)
        => Mutate(taskId, request?.Variables, true, ct);

    [FromHeader(Name = "Idempotency-Key")] public string? IdempotencyKey { get; set; }

    private async Task<IActionResult> Mutate(Guid taskId, IReadOnlyDictionary<string, object?>? variables, bool complete, CancellationToken ct)
    {
        var tenant = User.FindFirstValue("tenant_id");
        if (tenant is null) return Forbid();
        if (string.IsNullOrWhiteSpace(IdempotencyKey)) return BadRequest();
        if (complete) await gateway.CompleteInboxTaskAsync(tenant, taskId, IdempotencyKey, variables, ct);
        else await gateway.ClaimInboxTaskAsync(tenant, taskId, IdempotencyKey, ct);
        return NoContent();
    }
}
