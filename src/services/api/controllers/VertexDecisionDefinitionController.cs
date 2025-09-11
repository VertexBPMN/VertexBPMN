using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Api.Dto;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/vertex/decision-definition")]
public class VertexDecisionDefinitionController : ControllerBase
{
    private readonly IDecisionService _decisionService;

    public VertexDecisionDefinitionController(IDecisionService decisionService)
    {
        _decisionService = decisionService;
    }

    [HttpGet]
    public async IAsyncEnumerable<DecisionDefinitionDto> GetAll([FromQuery] string? key = null, [FromQuery] string? tenantId = null)
    {
        await foreach (var def in _decisionService.ListAsync(key, tenantId))
            yield return new DecisionDefinitionDto { Key = def.Key, Name = def.Name, TenantId = def.TenantId ?? string.Empty };
    }

    [HttpGet("{key}/xml")]
    public async Task<IActionResult> GetDmnXml(string key, [FromQuery] string? tenantId = null)
    {
        var def = await _decisionService.GetDecisionByKeyAsync(key, tenantId);
        if (def is null) return NotFound();
        // dmn-js expects { id, dmnXml }
        return Ok(new { id = def.Key, dmnXml = def.DmnXml });
    }

    public record UpdateDmnXmlRequest(string DmnXml);

    [HttpPut("{key}/xml")]
    public async Task<IActionResult> UpdateDmnXml(string key, [FromBody] UpdateDmnXmlRequest request, [FromQuery] string? tenantId = null)
    {
        var def = await _decisionService.GetDecisionByKeyAsync(key, tenantId);
        if (def is null) return NotFound();
        // For now, update in-memory only (like BPMN)
        if (_decisionService is VertexBPMN.Core.Services.DecisionService svc)
        {
            await svc.DeployAsync(key, def.Name, request.DmnXml, tenantId);
            return NoContent();
        }
        return StatusCode(501, "Update not supported for this service implementation.");
    }

    public class DecisionDefinitionDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
    }
}
