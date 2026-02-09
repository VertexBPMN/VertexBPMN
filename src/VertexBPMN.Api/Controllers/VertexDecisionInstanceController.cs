using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/vertex/decision-instance")]
public class VertexDecisionInstanceController : ControllerBase
{
    private readonly IDecisionService _decisionService;

    public VertexDecisionInstanceController(IDecisionService decisionService)
    {
        _decisionService = decisionService;
    }

    [HttpGet]
    public async IAsyncEnumerable<DecisionInstanceDto> GetAll([FromQuery] string? decisionKey = null, [FromQuery] string? tenantId = null)
    {
        await foreach (var inst in _decisionService.ListInstancesAsync(decisionKey, tenantId))
            yield return new DecisionInstanceDto { Id = inst.Id, DecisionKey = inst.DecisionDefinitionKey, Result = inst.OutputVariables.Values, EvaluatedAt = inst.EvaluationTime, TenantId = inst.TenantId ?? string.Empty };
    }

}
