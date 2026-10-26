using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Api.Services;

namespace VertexBPMN.Api.Controllers;
[ApiController]
[Route("api/applications")]
[Authorize(Policy = "ReadOnly")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public sealed class ApplicationController(IApplicationDefinitionService applications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApplicationDefinitionSummary>>> List([FromQuery] string? tenantId, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        return Ok(await applications.ListAsync(tenant, ct));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApplicationDefinitionMetadata>> Get(string id, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        var application = await applications.GetAsync(tenant, id, ct);
        return application is null ? NotFound() : Ok(application);
    }

    [HttpGet("{id}/revisions/{version:int}")]
    public async Task<ActionResult<ApplicationRevisionRecord>> GetRevision(string id, int version, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        var revision = await applications.GetRevisionAsync(tenant, id, version, ct);
        return revision is null ? NotFound() : Ok(revision);
    }

    [HttpGet("{id}/released-revision")]
    public async Task<ActionResult<ApplicationRevisionRecord>> GetReleasedRevision(string id, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        var revision = await applications.GetReleasedRevisionAsync(tenant, id, ct);
        return revision is null ? NotFound() : Ok(revision);
    }

    [HttpGet("{id}/released/data-sources/{bindingName}")]
    public async Task<IActionResult> GetReleasedDataSource(string id, string bindingName, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        var revision = await applications.GetReleasedRevisionAsync(tenant, id, ct);
        if (revision is null) { return NotFound(); }

        // Defense in depth: the API resolves the released revision and applies the same
        // narrow gate the BFF applies, so a sanctioned data-source-read binding resolves
        // and anything else is rejected (404) rather than served or forwarded.
        var read = ReleasedDataSourceReader.ResolveSanctionedRead(revision.ContentJson, bindingName);
        return read is null
            ? NotFound(UnsanctionedBinding(bindingName))
            : Ok(read);
    }

    [HttpGet("release-candidates/{candidateId}")]
    public async Task<ActionResult<ApplicationReleaseCandidateRecord>> GetReleaseCandidate(string candidateId, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        var candidate = await applications.GetReleaseCandidateAsync(tenant, candidateId, ct);
        return candidate is null ? NotFound() : Ok(candidate);
    }

    [HttpPost]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<ApplicationDefinitionMetadata>> Create([FromBody] ApplicationWriteRequest request, CancellationToken ct)
    {
        var tenant = Tenant(request.TenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        try
        {
            var application = await applications.CreateAsync(tenant, new(request.Key, request.Name, request.ExpectedVersion, request.Content), ct);
            return CreatedAtAction(nameof(Get), new { id = application.Id, tenantId = tenant }, application);
        }
        catch (Exception exception) when (exception is ApplicationDefinitionConflictException or ApplicationForeignTenantException or ApplicationValidationException or ArgumentException)
        { return Failure(exception); }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Update(string id, [FromBody] ApplicationWriteRequest request, CancellationToken ct)
    {
        var tenant = Tenant(request.TenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        try
        {
            return await applications.UpdateAsync(tenant, id, new(request.Key, request.Name, request.ExpectedVersion, request.Content), ct) ? NoContent() : NotFound();
        }
        catch (Exception exception) when (exception is ApplicationDefinitionConflictException or ApplicationForeignTenantException or ApplicationValidationException or ArgumentException)
        { return Failure(exception); }
    }

    [HttpPost("{id}/revisions/{version:int}/release-candidate")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<ApplicationReleaseCandidateRecord>> CreateReleaseCandidate(string id, int version, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        try
        {
            return Ok(await applications.CreateReleaseCandidateAsync(tenant, id, version, ct));
        }
        catch (Exception exception) when (exception is ApplicationDefinitionConflictException or ApplicationForeignTenantException or ApplicationValidationException or ArgumentException)
        { return Failure(exception); }
    }

    [HttpPost("release-candidates/{candidateId}/release")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Release(string candidateId, [FromQuery] string? tenantId, CancellationToken ct)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) { return Forbid(); }
        if (tenant is null) { return BadRequest(); }
        try
        {
            return await applications.ReleaseAsync(tenant, candidateId, ct) ? NoContent() : NotFound();
        }
        catch (Exception exception) when (exception is ApplicationDefinitionConflictException or ApplicationForeignTenantException or ApplicationValidationException or ArgumentException)
        { return Failure(exception); }
    }

    private JsonResult UnsanctionedBinding(string bindingName) => new(new
    {
        code = "apiUnsanctionedBinding",
        title = "Unsanctioned released binding.",
        detail = $"Data source '{bindingName}' is not a sanctioned released binding."
    })
    {
        StatusCode = StatusCodes.Status404NotFound
    };

    private ActionResult Failure(Exception exception) => exception switch
    {
        ApplicationDefinitionConflictException conflict => Conflict(new ProblemDetails { Detail = conflict.Message }),
        ApplicationForeignTenantException => Forbid(),
        _ => BadRequest(new ProblemDetails { Detail = exception.Message })
    };

    private string? Tenant(string? requested, out bool forbidden)
    {
        var claim = User.FindFirstValue("tenant_id");
        var value = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
        forbidden = !User.IsInRole("Admin") && value is not null && value != claim;
        return User.IsInRole("Admin") ? value ?? claim : claim;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type ApplicationWriteRequest retained for exposed HTTP action signatures and controller activation (ApplicationController).")]
    public sealed record ApplicationWriteRequest(string? TenantId, string Key, string Name, [property: JsonRequired] int ExpectedVersion, string Content);
}
