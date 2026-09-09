using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/variable")]
[Authorize(Policy = "TenantReadOnly")]
public class VertexVariableController : ControllerBase
{
    private readonly IRuntimeService _runtimeService;

    public VertexVariableController(IRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    [HttpGet("{processInstanceId}")]
    public async Task<ActionResult<IDictionary<string, VariableValueDto>>> GetVariables(Guid processInstanceId)
    {
        var instance = await _runtimeService.GetByIdAsync(processInstanceId);
        if (instance is null) return NotFound();
        if (!CanAccessTenant(instance.TenantId)) return NotFound();

        var variables = await _runtimeService.GetVariablesAsync(processInstanceId);
        if (variables == null) return NotFound();
        var result = new Dictionary<string, VariableValueDto>();
        foreach (var kv in variables)
        {
            result[kv.Key] = new VariableValueDto
            {
                Type = kv.Value?.GetType().Name ?? "Null",
                Value = kv.Value
            };
        }
        return result;
    }

    private bool CanAccessTenant(string? tenantId)
    {
        if (User.IsInRole("Admin")) return true;
        var claim = User.FindFirstValue("tenant_id");
        return !string.IsNullOrWhiteSpace(claim) && string.Equals(tenantId, claim, StringComparison.Ordinal);
    }
}
