using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/polling-triggers")]
[Authorize]
public sealed class PollingTriggerController(IPollingTriggerService triggerService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PollingTriggerInfo>>> List(
        [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        return Ok(await triggerService.ListAsync(effectiveTenantId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PollingTriggerInfo>> Get(
        Guid id, [FromQuery] string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var trigger = await triggerService.GetAsync(id, effectiveTenantId, cancellationToken);
        return trigger is null ? NotFound() : Ok(trigger);
    }

    [HttpPost]
    [Authorize(Policy = "ProcessManager")]
    [ProducesResponseType(typeof(PollingTriggerCreated), StatusCodes.Status201Created)]
    public async Task<ActionResult<PollingTriggerCreated>> Create(
        [FromBody] CreatePollingTriggerRequest request, CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(request.TenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        try
        {
            var created = await triggerService.CreateAsync(
                new PollingTriggerWriteRequest(
                    request.Name, request.ProcessDefinitionKey, request.ConnectorType,
                    request.ConnectorAttributesJson, request.CredentialId, request.IntervalSeconds, request.Enabled),
                effectiveTenantId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = created.Trigger.Id, tenantId = effectiveTenantId }, created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid polling trigger", Detail = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdatePollingTriggerRequest request, [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var updated = await triggerService.UpdateAsync(
            id,
            new PollingTriggerWriteRequest(
                request.Name ?? string.Empty, request.ProcessDefinitionKey ?? string.Empty, request.ConnectorType ?? string.Empty,
                request.ConnectorAttributesJson, request.CredentialId, request.IntervalSeconds, request.Enabled),
            null, effectiveTenantId, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        return await triggerService.DeleteAsync(id, effectiveTenantId, cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>Polls a trigger synchronously on demand (useful for testing without waiting for the scheduler interval).</summary>
    [HttpPost("{id:guid}/poll-now")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<PollingTriggerInfo>> PollNow(Guid id, [FromQuery] string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var trigger = await triggerService.PollNowAsync(id, effectiveTenantId, cancellationToken);
        return trigger is null ? NotFound() : Ok(trigger);
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");

    public sealed record CreatePollingTriggerRequest(
        string Name, string ProcessDefinitionKey, string ConnectorType,
        string? ConnectorAttributesJson = null, string? CredentialId = null, int? IntervalSeconds = null, bool? Enabled = null,
        string? TenantId = null);

    public sealed record UpdatePollingTriggerRequest(
        string? Name = null, string? ProcessDefinitionKey = null, string? ConnectorType = null,
        string? ConnectorAttributesJson = null, string? CredentialId = null, int? IntervalSeconds = null, bool? Enabled = null);
}
