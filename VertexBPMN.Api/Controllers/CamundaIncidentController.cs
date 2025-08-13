using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Api.Dto;
using CoreIncident = VertexBPMN.Core.Domain.Incident;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
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
    public async IAsyncEnumerable<IncidentDto> GetAll()
    {
        await foreach (var incident in _incidentService.ListAsync())
            yield return ToDto(incident);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<IncidentDto>> GetById(Guid id)
    {
        var incident = await _incidentService.GetByIdAsync(id);
        if (incident is null) return NotFound();
        return ToDto(incident);
    }

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
