using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/history")]
[Authorize]
public class HistoryController : ControllerBase
{
    private readonly IHistoryService _historyService;

    public HistoryController(IHistoryService historyService)
    {
        _historyService = historyService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HistoryEvent>>> List([FromQuery] string? tenantId = null, CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();

        var events = new List<HistoryEvent>();
        await foreach (var historyEvent in _historyService.ListAsync(effectiveTenantId, cancellationToken))
        {
            events.Add(historyEvent);
        }

        return events;
    }

    [HttpGet("by-process-instance/{processInstanceId}")]
    public async Task<ActionResult<IReadOnlyList<HistoryEvent>>> ListByProcessInstance(
        Guid processInstanceId,
        [FromQuery] string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();

        var events = new List<HistoryEvent>();
        await foreach (var historyEvent in _historyService.ListByProcessInstanceAsync(processInstanceId, effectiveTenantId, cancellationToken))
        {
            events.Add(historyEvent);
        }

        return events;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<HistoryEvent>> GetById(Guid id)
    {
        var evt = await _historyService.GetByIdAsync(id);
        if (evt is null) return NotFound();
        if (!CanAccessTenant(evt.TenantId)) return Forbid();
        return evt;
    }

    private string? ResolveTenantId(string? requestedTenantId)
    {
        if (User.IsInRole("Admin"))
            return string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();

        return User.FindFirstValue("tenant_id");
    }

    private bool CanAccessTenant(string? tenantId) =>
        User.IsInRole("Admin") || string.Equals(User.FindFirstValue("tenant_id"), tenantId, StringComparison.Ordinal);
}
