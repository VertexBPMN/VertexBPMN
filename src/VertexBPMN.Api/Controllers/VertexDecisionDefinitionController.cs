using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VertexBPMN.Application;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/vertex/decision-definition")]
[Authorize]
public class VertexDecisionDefinitionController : ControllerBase
{
    private readonly IDecisionService _decisionService;

    public VertexDecisionDefinitionController(IDecisionService decisionService)
    {
        _decisionService = decisionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DecisionDefinitionDto>>> GetAll([FromQuery] string? key = null, [FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var definitions = new List<DecisionDefinitionDto>();
        await foreach (var def in _decisionService.ListAsync(key, effectiveTenantId))
            definitions.Add(new DecisionDefinitionDto { Key = def.Key, Name = def.Name, TenantId = def.TenantId ?? string.Empty });
        return definitions;
    }

    [HttpGet("{key}/xml")]
    public async Task<IActionResult> GetDmnXml(string key, [FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var def = await _decisionService.GetDecisionByKeyAsync(key, effectiveTenantId);
        if (def is null) return NotFound();
        // dmn-js expects { id, dmnXml }
        return Ok(new { id = def.Key, dmnXml = def.DmnXml });
    }

    public record UpdateDmnXmlRequest(string DmnXml);

    [HttpPut("{key}/xml")]
    public async Task<IActionResult> UpdateDmnXml(string key, [FromBody] UpdateDmnXmlRequest request, [FromQuery] string? tenantId = null)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
        {
            Title = "DMN XML update is unavailable",
            Detail = "The decision contract does not provide a durable XML update operation."
        });
    }

    public class DecisionDefinitionDto
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");
}
