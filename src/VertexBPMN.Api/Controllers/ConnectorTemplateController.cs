using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/connector-templates")]
[Authorize(Policy = "ReadOnly")]
public sealed class ConnectorTemplateController(IConnectorTemplateService templates) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectorTemplateMetadata>>> List([FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) return Forbid();
        return tenant is null ? BadRequest() : Ok(await templates.ListAsync(tenant, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ConnectorTemplateMetadata>> Get(string id, [FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest();
        var template = await templates.GetAsync(tenant, id, cancellationToken);
        return template is null ? NotFound() : Ok(template);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ConnectorTemplateMetadata>> Create([FromBody] TemplateRequest request, CancellationToken cancellationToken)
    {
        var tenant = Tenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest();
        try
        {
            var template = await templates.CreateAsync(tenant, request.ToWriteRequest(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = template.Id, tenantId = tenant }, template);
        }
        catch (ConnectorTemplateConflictException exception) { return Conflict(new ProblemDetails { Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(string id, [FromBody] TemplateRequest request, CancellationToken cancellationToken)
    {
        var tenant = Tenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest();
        try { return await templates.UpdateAsync(tenant, id, request.ToWriteRequest(), cancellationToken) ? NoContent() : NotFound(); }
        catch (ConnectorTemplateConflictException exception) { return Conflict(new ProblemDetails { Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(string id, [FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        var tenant = Tenant(tenantId, out var forbidden);
        if (forbidden) return Forbid();
        return tenant is null ? BadRequest() : await templates.DeleteAsync(tenant, id, cancellationToken) ? NoContent() : NotFound();
    }

    private string? Tenant(string? requested, out bool forbidden)
    {
        var tenant = string.IsNullOrWhiteSpace(requested) ? null : requested.Trim();
        var claimTenant = User.FindFirstValue("tenant_id");
        forbidden = !User.IsInRole("Admin") && tenant is not null && !string.Equals(tenant, claimTenant, StringComparison.Ordinal);
        return User.IsInRole("Admin") ? tenant ?? claimTenant : claimTenant;
    }

    public sealed record TemplateRequest(string? TenantId, string Name, string Category, IReadOnlyList<string> AppliesTo, string Runtime, string? Icon, IReadOnlyList<ConnectorTemplateProperty> Properties)
    {
        public ConnectorTemplateWriteRequest ToWriteRequest() => new(Name, Category, AppliesTo, Runtime, Icon, Properties);
    }
}
