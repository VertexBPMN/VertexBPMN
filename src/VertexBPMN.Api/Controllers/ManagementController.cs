using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/management")]
public class ManagementController : ControllerBase
{
    private readonly IManagementService _managementService;
    private readonly IRuntimeService _runtimeService;

    public ManagementController(IManagementService managementService, IRuntimeService runtimeService)
    {
        _managementService = managementService;
        _runtimeService = runtimeService;
    }

    [HttpPost("suspend-process-instance/{id}")]
    public async Task<IActionResult> SuspendProcessInstance(Guid id, [FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenant(id, tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        if (effectiveTenantId is not null && !await CanAccessInstanceAsync(id, effectiveTenantId)) return Forbid();
        await _managementService.SuspendProcessInstanceAsync(id, effectiveTenantId);
        return NoContent();
    }

    [HttpPost("resume-process-instance/{id}")]
    public async Task<IActionResult> ResumeProcessInstance(Guid id, [FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenant(id, tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        if (effectiveTenantId is not null && !await CanAccessInstanceAsync(id, effectiveTenantId)) return Forbid();
        await _managementService.ResumeProcessInstanceAsync(id, effectiveTenantId);
        return NoContent();
    }

    [HttpPost("delete-process-instance/{id}")]
    public async Task<IActionResult> DeleteProcessInstance(Guid id, [FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenant(id, tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        if (effectiveTenantId is not null && !await CanAccessInstanceAsync(id, effectiveTenantId)) return Forbid();
        await _managementService.DeleteProcessInstanceAsync(id, effectiveTenantId);
        return NoContent();
    }

    private string? ResolveTenant(Guid instanceId, string? requestedTenantId)
    {
        if (!User.IsInRole("Admin"))
            return User.FindFirst("tenant_id")?.Value;

        return string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();
    }

    private async Task<bool> CanAccessInstanceAsync(Guid instanceId, string tenantId)
    {
        var instance = await _runtimeService.GetByIdAsync(instanceId);
        return instance is not null && string.Equals(instance.TenantId, tenantId, StringComparison.Ordinal);
    }
    [HttpGet("metrics")]
    public async Task<ActionResult<IDictionary<string, object>>> GetMetrics()
    {
        var metrics = await _managementService.GetMetricsAsync();
        return Ok(metrics);
    }
}
