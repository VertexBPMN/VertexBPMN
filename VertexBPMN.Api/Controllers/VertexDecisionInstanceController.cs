using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Api.Dto;

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
            yield return new DecisionInstanceDto { Id = inst.Id, DecisionKey = inst.DecisionKey, Result = inst.Result, EvaluatedAt = inst.EvaluatedAt, TenantId = inst.TenantId ?? string.Empty };
    }

    public class DecisionInstanceDto
    {
        public string Id { get; set; } = string.Empty;
        public string DecisionKey { get; set; } = string.Empty;
        public object? Result { get; set; }
        public DateTime EvaluatedAt { get; set; }
        public string TenantId { get; set; } = string.Empty;
    }
}
