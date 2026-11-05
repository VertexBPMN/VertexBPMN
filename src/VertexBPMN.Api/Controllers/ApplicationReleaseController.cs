using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// LC10 release activation surface: Validate → Prepare → Activate → Status → Rollback.
/// Retries reuse the SAME operation identity (idempotent). Rollback and Cleanup are separately
/// named requests so isolated test data can be cleaned without touching the rollback pointer.
/// All endpoints are tenant-checked via the application id + tenant claim.
/// </summary>
[ApiController]
[Route("api/applications")]
[Authorize(Policy = "ReadOnly")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public sealed class ApplicationReleaseController(IApplicationReleaseService releases) : ControllerBase
{
    [HttpPost("releases/validate")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Validate([FromQuery] string? tenantId, [FromBody] ValidateArchiveRequest request, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        try
        {
            var diagnostics = await releases.ValidateArchiveAsync(tenant, request.OpenArchiveStream(), ct);
            return Ok(new { diagnostics });
        }
        catch (ApplicationValidationException exception)
        { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpPost("{id}/releases/prepare")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Prepare(string id, [FromQuery] string? tenantId, [FromBody] PrepareReleaseRequest request, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        try
        {
            var release = await releases.PrepareAsync(request.OperationId, tenant, id, request.Revision, request.ManifestJson, ct);
            return StatusCode(StatusCodes.Status201Created, JsonDocument.Parse(release.ToStatusJson()));
        }
        catch (ApplicationReleaseCompatibilityException exception)
        { return BadRequest(new ProblemDetails { Detail = exception.Message, Extensions = { ["release"] = exception.Release?.ToStatusJson() ?? string.Empty } }); }
        catch (ApplicationForeignTenantException)
        { return Forbid(); }
        catch (ApplicationValidationException exception)
        { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpPost("{id}/releases/activate")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Activate(string id, [FromQuery] string? tenantId, [FromBody] ActivateReleaseRequest request, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        try
        {
            var release = await releases.ActivateAsync(request.OperationId, tenant, id, ct);
            return Ok(JsonDocument.Parse(release.ToStatusJson()));
        }
        catch (ApplicationReleaseCompatibilityException exception)
        { return BadRequest(new ProblemDetails { Detail = exception.Message, Extensions = { ["release"] = exception.Release?.ToStatusJson() ?? string.Empty } }); }
        catch (ApplicationValidationException exception)
        { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpGet("{id}/releases/status")]
    public async Task<IActionResult> Status(string id, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        var release = await releases.GetStatusAsync(tenant, id, ct);
        return release is null ? NotFound(new ProblemDetails { Detail = "No release has been prepared for this application." })
            : Ok(JsonDocument.Parse(release.ToStatusJson()));
    }

    [HttpPost("{id}/releases/rollback")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Rollback(string id, [FromQuery] string? tenantId, [FromBody] OperateReleaseRequest request, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        try
        {
            var release = await releases.RollbackAsync(request.OperationId, tenant, id, ct);
            return release is null ? NotFound(new ProblemDetails { Detail = "No rollback target available." })
                : Ok(JsonDocument.Parse(release.ToStatusJson()));
        }
        catch (ApplicationValidationException exception)
        { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpPost("{id}/releases/cleanup")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Cleanup(string id, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        await releases.CleanupAsync(tenant, id, ct);
        return NoContent();
    }

    [HttpGet("{id}/releases/active-start")]
    public async Task<IActionResult> ActiveStart(string id, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        var processDefinitionId = await releases.ResolveActiveProcessDefinitionIdAsync(tenant, id, ct);
        return processDefinitionId is null
            ? NotFound(new ProblemDetails { Detail = "No active release with a bound start entry." })
            : Ok(new { processDefinitionId });
    }

    private string? Tenant(string? requested, out bool forbidden)
    {
        var claim = User.FindFirstValue("tenant_id");
        var value = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
        forbidden = !User.IsInRole("Admin") && value is not null && value != claim;
        return User.IsInRole("Admin") ? value ?? claim : claim;
    }

    public sealed record ValidateArchiveRequest(string ArchiveBase64)
    {
        public System.IO.Stream OpenArchiveStream() => new System.IO.MemoryStream(System.Convert.FromBase64String(ArchiveBase64));
    }

    public sealed record PrepareReleaseRequest([property: System.Text.Json.Serialization.JsonRequired] string OperationId, [property: System.Text.Json.Serialization.JsonRequired] int Revision, [property: System.Text.Json.Serialization.JsonRequired] string ManifestJson);

    public sealed record ActivateReleaseRequest([property: System.Text.Json.Serialization.JsonRequired] string OperationId);

    public sealed record OperateReleaseRequest([property: System.Text.Json.Serialization.JsonRequired] string OperationId);
}
