using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/case-definitions")]
[Authorize]
public sealed class CaseDefinitionController(ICaseExecutionRuntime cases) : ControllerBase
{
    [HttpPost("deploy")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<CaseDefinitionRecord>> Deploy([FromBody] DeployRequest request, CancellationToken cancellationToken)
    {
        var tenant = Tenant(request.TenantId);
        if (tenant is null) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.CmmnXml)) return BadRequest();
        try
        {
            var definition = await cases.DeployAsync(request.Key, request.Name, request.CmmnXml, tenant, cancellationToken);
            return CreatedAtAction(nameof(Get), new { key = definition.Key, tenantId = tenant }, definition);
        }
        catch (InvalidOperationException) { return Conflict(); }
    }

    [HttpGet("{key}")]
    public async Task<ActionResult<CaseDefinitionRecord>> Get(string key, [FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        var tenant = Tenant(tenantId); if (tenant is null) return Forbid();
        var definition = await cases.GetDefinitionAsync(key, tenant, cancellationToken);
        return definition is null ? NotFound() : Ok(definition);
    }

    [HttpPost("{key}/start")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<CaseRunResponse>> Start(string key, [FromBody] StartRequest request, CancellationToken cancellationToken)
    {
        var tenant = Tenant(request.TenantId); if (tenant is null) return Forbid();
        try
        {
            var result = await cases.StartAsync(key, tenant, request.CaseFile, cancellationToken);
            return Ok(ToResponse(result));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("instances/{caseInstanceId:guid}")]
    public async Task<ActionResult<CaseInstanceRecord>> GetInstance(Guid caseInstanceId, [FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        var tenant = Tenant(tenantId); if (tenant is null) return Forbid();
        var instance = await cases.GetInstanceAsync(caseInstanceId, tenant, cancellationToken);
        return instance is null ? NotFound() : Ok(instance);
    }

    [HttpGet("instances/{caseInstanceId:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<CaseHistoryEntry>>> GetHistory(
        Guid caseInstanceId,
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = Tenant(tenantId); if (tenant is null) return Forbid();
        var instance = await cases.GetInstanceAsync(caseInstanceId, tenant, cancellationToken);
        if (instance is null) return NotFound();
        return Ok(await cases.GetHistoryAsync(caseInstanceId, tenant, cancellationToken));
    }

    [HttpPost("instances/{caseInstanceId:guid}/plan-items/{planItemId}/complete")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<CaseRunResponse>> CompletePlanItem(Guid caseInstanceId, string planItemId, [FromBody] CompletePlanItemRequest request, CancellationToken cancellationToken)
    {
        var tenant = Tenant(request.TenantId); if (tenant is null) return Forbid();
        try { return Ok(ToResponse(await cases.CompletePlanItemAsync(caseInstanceId, planItemId, request.CaseFileUpdates, tenant, cancellationToken))); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(exception.Message); }
    }

    [HttpPost("instances/{caseInstanceId:guid}/events/{eventId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<CaseRunResponse>> TriggerEvent(Guid caseInstanceId, string eventId, [FromBody] TriggerEventRequest request, CancellationToken cancellationToken)
    {
        var tenant = Tenant(request.TenantId); if (tenant is null) return Forbid();
        try { return Ok(ToResponse(await cases.TriggerUserEventAsync(caseInstanceId, eventId, request.EventData, tenant, cancellationToken))); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(exception.Message); }
    }

    [HttpPut("instances/{caseInstanceId:guid}/case-file/{itemId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<CaseRunResponse>> UpdateCaseFile(Guid caseInstanceId, string itemId, [FromBody] UpdateCaseFileRequest request, CancellationToken cancellationToken)
    {
        var tenant = Tenant(request.TenantId); if (tenant is null) return Forbid();
        try { return Ok(ToResponse(await cases.UpdateCaseFileItemAsync(caseInstanceId, itemId, request.Value, tenant, cancellationToken))); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("instances/{caseInstanceId:guid}/discretionary-items/{planItemId}/activate")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<CaseRunResponse>> ActivateDiscretionaryItem(
        Guid caseInstanceId,
        string planItemId,
        [FromBody] TenantRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = Tenant(request.TenantId); if (tenant is null) return Forbid();
        try { return Ok(ToResponse(await cases.ActivateDiscretionaryItemAsync(caseInstanceId, planItemId, tenant, cancellationToken))); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException exception) { return Conflict(exception.Message); }
    }

    private static CaseRunResponse ToResponse(CaseExecutionResult result) =>
        new(result.Instance.Id, result.Instance.CaseDefinitionId, result.Instance.CaseDefinitionKey, result.Instance.State, result.Trace);

    private string? Tenant(string? requested) => User.IsInRole("Admin") ? requested?.Trim() ?? "default" : User.FindFirstValue("tenant_id");
    public sealed record DeployRequest(string Key, string Name, string CmmnXml, string? TenantId);
    public sealed record StartRequest(string? TenantId, Dictionary<string, object>? CaseFile = null);
    public sealed record CompletePlanItemRequest(string? TenantId, Dictionary<string, object>? CaseFileUpdates = null);
    public sealed record TriggerEventRequest(string? TenantId, Dictionary<string, object>? EventData = null);
    public sealed record UpdateCaseFileRequest(string? TenantId, object? Value);
    public sealed record TenantRequest(string? TenantId);
    public sealed record CaseRunResponse(Guid CaseInstanceId, string CaseDefinitionId, string Key, string State, IReadOnlyList<string> Trace);
}
