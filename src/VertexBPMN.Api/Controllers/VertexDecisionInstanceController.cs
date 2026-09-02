using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/vertex/decision-instance")]
[Authorize]
public class VertexDecisionInstanceController : ControllerBase
{
    private readonly IDecisionService _decisionService;

    public VertexDecisionInstanceController(IDecisionService decisionService)
    {
        _decisionService = decisionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DecisionInstanceDto>>> GetAll([FromQuery] string? decisionKey = null, [FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var instances = new List<DecisionInstanceDto>();
        await foreach (var inst in _decisionService.ListInstancesAsync(decisionKey, effectiveTenantId))
            instances.Add(new DecisionInstanceDto { Id = inst.Id, DecisionKey = inst.DecisionDefinitionKey, Result = inst.OutputVariables.Values, EvaluatedAt = inst.EvaluationTime, TenantId = inst.TenantId ?? string.Empty });
        return instances;
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");

}
