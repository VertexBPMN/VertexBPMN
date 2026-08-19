using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/forms")]
[Authorize(Policy = "ReadOnly")]
public sealed class FormController(IFormDefinitionService forms) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<FormDefinitionMetadata>>> List([FromQuery] string? tenantId, CancellationToken ct) { var tenant = Tenant(tenantId, out var forbidden); return forbidden ? Forbid() : tenant is null ? BadRequest() : Ok(await forms.ListAsync(tenant, ct)); }
    [HttpGet("{id}")] public async Task<ActionResult<FormDefinitionMetadata>> Get(string id, [FromQuery] string? tenantId, CancellationToken ct) { var tenant = Tenant(tenantId, out var forbidden); if (forbidden) return Forbid(); if (tenant is null) return BadRequest(); var form = await forms.GetAsync(tenant, id, ct); return form is null ? NotFound() : Ok(form); }
    [HttpPost] [Authorize(Policy = "ProcessManager")] public async Task<ActionResult<FormDefinitionMetadata>> Create([FromBody] Write request, CancellationToken ct) { var tenant = Tenant(request.TenantId, out var forbidden); if (forbidden) return Forbid(); if (tenant is null) return BadRequest(); try { var form = await forms.CreateAsync(tenant, new(request.Key, request.Name, request.Schema), ct); return CreatedAtAction(nameof(Get), new { id = form.Id, tenantId = tenant }, form); } catch (FormDefinitionConflictException e) { return Conflict(new ProblemDetails { Detail = e.Message }); } catch (ArgumentException e) { return BadRequest(new ProblemDetails { Detail = e.Message }); } }
    [HttpPut("{id}")] [Authorize(Policy = "ProcessManager")] public async Task<IActionResult> Update(string id, [FromBody] Write request, CancellationToken ct) { var tenant = Tenant(request.TenantId, out var forbidden); if (forbidden) return Forbid(); if (tenant is null) return BadRequest(); try { return await forms.UpdateAsync(tenant, id, new(request.Key, request.Name, request.Schema), ct) ? NoContent() : NotFound(); } catch (FormDefinitionConflictException e) { return Conflict(new ProblemDetails { Detail = e.Message }); } catch (ArgumentException e) { return BadRequest(new ProblemDetails { Detail = e.Message }); } }
    [HttpDelete("{id}")] [Authorize(Policy = "ProcessManager")] public async Task<IActionResult> Delete(string id, [FromQuery] string? tenantId, CancellationToken ct) { var tenant = Tenant(tenantId, out var forbidden); return forbidden ? Forbid() : tenant is null ? BadRequest() : await forms.DeleteAsync(tenant, id, ct) ? NoContent() : NotFound(); }
    private string? Tenant(string? requested, out bool forbidden) { var claim = User.FindFirstValue("tenant_id"); var value = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim(); forbidden = !User.IsInRole("Admin") && value is not null && value != claim; return User.IsInRole("Admin") ? value ?? claim : claim; }
    public sealed record Write(string? TenantId, string Key, string Name, string Schema);
}
