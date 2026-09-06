using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using VertexBPMN.Application.Import;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/import/openapi")]
[Authorize(Policy = "ProcessManager")]
public sealed class OpenApiImportController(IOpenApiConnectorTemplateImporter importer, IConnectorTemplateService connectorTemplateService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OpenApiImportResult>> Import([FromBody] OpenApiImportRequest request, CancellationToken cancellationToken)
    {
        var tenant = ResolveTenant(request.TenantId, out var forbidden);
        if (forbidden) return Forbid();
        if (tenant is null) return BadRequest(new ProblemDetails { Title = "TenantId is required." });
        try
        {
            var result = importer.Import(request.OpenApiJson, tenant);
            foreach (var template in result.Templates)
                await connectorTemplateService.CreateAsync(tenant, template, cancellationToken);
            return Ok(result.Report);
        }
        catch (ArgumentException exception) { return BadRequest(new ProblemDetails { Detail = exception.Message }); }
        catch (JsonException exception) { return BadRequest(new ProblemDetails { Detail = $"Invalid OpenAPI JSON: {exception.Message}" }); }
    }

    private string? ResolveTenant(string? requestedTenantId, out bool forbidden)
    {
        forbidden = false;
        if (User.IsInRole("Admin")) return string.IsNullOrWhiteSpace(requestedTenantId) ? "default" : requestedTenantId.Trim();
        var tenant = User.FindFirstValue("tenant_id");
        if (string.IsNullOrWhiteSpace(tenant)) forbidden = true;
        return tenant;
    }

    public sealed record OpenApiImportRequest(string OpenApiJson, string? TenantId);
}
