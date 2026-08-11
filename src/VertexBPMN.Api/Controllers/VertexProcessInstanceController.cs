using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;
using CoreInstance = VertexBPMN.Domain.Entities.ProcessInstance;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
[ApiController]
[Route("api/vertex/process-instance")]
[Authorize]
public class VertexProcessInstanceController : ControllerBase
{
    private readonly IRuntimeService _runtimeService;

    public VertexProcessInstanceController(IRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProcessInstanceDto>>> GetAll([FromQuery] Guid? processDefinitionId = null, [FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var instances = new List<ProcessInstanceDto>();
        await foreach (var instance in _runtimeService.ListAsync(processDefinitionId, effectiveTenantId))
            instances.Add(ToDto(instance));
        return instances;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessInstanceDto>> GetById(Guid id)
    {
        var instance = await _runtimeService.GetByIdAsync(id);
        if (instance is null) return NotFound();
        if (!CanAccessTenant(instance.TenantId)) return Forbid();
        return ToDto(instance);
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");

    private bool CanAccessTenant(string? tenantId) =>
        User.IsInRole("Admin") || string.Equals(User.FindFirstValue("tenant_id"), tenantId, StringComparison.Ordinal);

    private static ProcessInstanceDto ToDto(CoreInstance i) => new()
    {
        Id = i.Id,
        ProcessDefinitionId = i.ProcessDefinitionId.ToString(),
        BusinessKey = i.BusinessKey ?? string.Empty,
        TenantId = i.TenantId ?? string.Empty,
        // ...mapping für weitere Felder nach Camunda-DTO...
    };
}
