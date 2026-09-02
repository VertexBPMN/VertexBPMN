using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/connectors")]
[Authorize(Policy = "ReadOnly")]
public sealed class ConnectorController(IConnectorService connectorService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectorMetadata>>> List([FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(tenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });
        return Ok(await connectorService.ListAsync(tenant, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ConnectorMetadata>> Get(string id, [FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(tenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });
        var connector = await connectorService.GetAsync(tenant, id, cancellationToken);
        return connector is null ? NotFound() : Ok(connector);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ConnectorMetadata>> Create([FromBody] ConnectorRequest request, CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });
        try
        {
            var connector = await connectorService.CreateAsync(tenant, request.ToWriteRequest(), cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = connector.Id, tenantId = tenant }, connector);
        }
        catch (ConnectorConflictException exception) { return Conflict(new ProblemDetails { Title = "Connector already exists.", Detail = exception.Message }); }
        catch (ConnectorCredentialException exception) { return UnprocessableEntity(new ProblemDetails { Title = "Invalid credential reference.", Detail = exception.Message }); }
        catch (ConnectorTemplateReferenceException exception) { return UnprocessableEntity(new ProblemDetails { Title = "Invalid connector template reference.", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Invalid connector.", Detail = exception.Message }); }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(string id, [FromBody] ConnectorRequest request, CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });
        try
        {
            return await connectorService.UpdateAsync(tenant, id, request.ToWriteRequest(), cancellationToken) ? NoContent() : NotFound();
        }
        catch (ConnectorConflictException exception) { return Conflict(new ProblemDetails { Title = "Connector already exists.", Detail = exception.Message }); }
        catch (ConnectorCredentialException exception) { return UnprocessableEntity(new ProblemDetails { Title = "Invalid credential reference.", Detail = exception.Message }); }
        catch (ConnectorTemplateReferenceException exception) { return UnprocessableEntity(new ProblemDetails { Title = "Invalid connector template reference.", Detail = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Title = "Invalid connector.", Detail = exception.Message }); }
    }

    [HttpPut("{id}/enabled")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetEnabled(string id, [FromBody] SetConnectorEnabledRequest request, CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });
        return await connectorService.SetEnabledAsync(tenant, id, request.Enabled, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPost("{id}/test")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<ConnectorTestResult>> Test(string id, [FromBody] ConnectorTestRequest request, CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });
        var result = await connectorService.TestAsync(tenant, id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(string id, [FromQuery] string? tenantId, CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(tenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });
        return await connectorService.DeleteAsync(tenant, id, cancellationToken) ? NoContent() : NotFound();
    }

    private string? ResolveTenant(string? requestedTenantId, out bool forbidden)
    {
        var requested = string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();
        var claimTenant = User.FindFirstValue("tenant_id");
        forbidden = !User.IsInRole("Admin") && requested is not null && !string.Equals(requested, claimTenant, StringComparison.Ordinal);
        return User.IsInRole("Admin") ? requested ?? claimTenant : claimTenant;
    }

    public sealed record ConnectorRequest(
        string? TenantId,
        string Name,
        string Type,
        string? Description,
        string? Endpoint,
        string? CredentialId,
        string? TemplateId,
        bool Enabled = true)
    {
        public ConnectorWriteRequest ToWriteRequest() => new(Name, Type, Description, Endpoint, CredentialId, TemplateId, Enabled);
    }

    public sealed record SetConnectorEnabledRequest(string? TenantId, bool Enabled);
    public sealed record ConnectorTestRequest(string? TenantId);
}
