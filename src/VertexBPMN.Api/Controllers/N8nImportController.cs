using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VertexBPMN.Application.Import;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/import/n8n")]
[Authorize(Policy = "ProcessManager")]
public sealed class N8nImportController(IN8nWorkflowImporter importer, ICredentialService credentialService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<N8nImportResult>> Import([FromBody] N8nImportRequest request, CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });
        try { return Ok(importer.Import(request.WorkflowJson, await credentialService.ListAsync(tenant, cancellationToken))); }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
        catch (System.Text.Json.JsonException exception) { return BadRequest(new ProblemDetails { Detail = $"Invalid n8n JSON: {exception.Message}" }); }
    }

    private string? ResolveTenant(string? requestedTenantId, out bool forbidden)
    {
        forbidden = false;
        if (User.IsInRole("Admin")) return string.IsNullOrWhiteSpace(requestedTenantId) ? "default" : requestedTenantId.Trim();
        var tenant = User.FindFirstValue("tenant_id");
        if (string.IsNullOrWhiteSpace(tenant)) forbidden = true;
        return tenant;
    }

    public sealed record N8nImportRequest(string WorkflowJson, string? TenantId);
}
