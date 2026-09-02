using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;
using CoreIncident = VertexBPMN.Domain.Entities.Incident;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
[ApiController]
[Route("api/vertex/incident")]
[Authorize]
public class VertexIncidentController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public VertexIncidentController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IncidentDto>>> GetAll([FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var incidents = new List<IncidentDto>();
        await foreach (var incident in _incidentService.ListAsync(effectiveTenantId))
            incidents.Add(ToDto(incident));
        return incidents;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<IncidentDto>> GetById(Guid id)
    {
        var incident = await _incidentService.GetByIdAsync(id);
        if (incident is null) return NotFound();
        if (!CanAccessTenant(incident.TenantId)) return Forbid();
        return ToDto(incident);
    }

    [HttpPost("{id}/resolve")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveIncidentRequest request)
    {
        var incident = await _incidentService.GetByIdAsync(id);
        if (incident is null) return NotFound();
        if (!CanAccessTenant(incident.TenantId, request.TenantId)) return Forbid();
        await _incidentService.ResolveAsync(
            id,
            incident.TenantId,
            Request.Headers["Idempotency-Key"].FirstOrDefault());
        return NoContent();
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");

    private bool CanAccessTenant(string? tenantId, string? requestedTenantId = null)
    {
        if (User.IsInRole("Admin"))
            return string.IsNullOrWhiteSpace(requestedTenantId)
                   || string.Equals(requestedTenantId, tenantId, StringComparison.Ordinal);
        return string.Equals(User.FindFirstValue("tenant_id"), tenantId, StringComparison.Ordinal);
    }

    public sealed record ResolveIncidentRequest(string? TenantId = null);

    private static IncidentDto ToDto(CoreIncident i) => new()
    {
        Id = i.Id.ToString(),
        ProcessInstanceId = i.ProcessInstanceId.ToString(),
        IncidentType = i.Type,
        Message = i.Message,
        IncidentTimestamp = i.CreatedAt,
        TenantId = i.TenantId ?? string.Empty,
        // ...mapping für weitere Felder nach Camunda-DTO...
    };
}
