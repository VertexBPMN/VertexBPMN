using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
[ApiController]
[Route("api/vertex/history/task")]
[Authorize]
public class VertexHistoricTaskInstanceController : ControllerBase
{
    private readonly IHistoryService _historyService;

    public VertexHistoricTaskInstanceController(IHistoryService historyService)
    {
        _historyService = historyService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HistoricTaskInstanceDto>>> GetAll([FromQuery] Guid? processInstanceId = null, [FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var events = new List<HistoricTaskInstanceDto>();
        await foreach (var evt in _historyService.ListHistoricTasksAsync(processInstanceId, effectiveTenantId))
            events.Add(ToDto(evt));
        return events;
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");

    private static HistoricTaskInstanceDto ToDto(HistoryEvent e) => new()
    {
        Id = e.Id,
        ProcessInstanceId = e.ProcessInstanceId.ToString(),
        StartTime = e.Timestamp,
        // ...mapping für weitere Felder nach Camunda-DTO...
    };
}
