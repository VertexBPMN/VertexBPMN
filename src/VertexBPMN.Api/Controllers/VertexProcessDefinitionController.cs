using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;
using CoreDef = VertexBPMN.Domain.Entities.ProcessDefinition;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/vertex/process-definition")]
[Authorize]
public class VertexProcessDefinitionController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;

    public VertexProcessDefinitionController(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProcessDefinitionDto>>> GetAll([FromQuery] string? key = null, [FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var definitions = new List<ProcessDefinitionDto>();
        await foreach (var def in _repositoryService.ListAsync(key, effectiveTenantId))
            definitions.Add(ToDto(def));
        return definitions;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessDefinitionDto>> GetById(Guid id)
    {
        var def = await _repositoryService.GetByIdAsync(id);
        if (def is null) return NotFound();
        if (!CanAccessTenant(def.TenantId)) return Forbid();
        return ToDto(def);
    }

    /// <summary>
    /// Returns the BPMN XML for a process definition (bpmn-js compatible).
    /// </summary>
    [HttpGet("{id}/xml")]
    public async Task<IActionResult> GetXml(Guid id)
    {
        var def = await _repositoryService.GetByIdAsync(id);
        if (def is null) return NotFound();
        if (!CanAccessTenant(def.TenantId)) return Forbid();
        // bpmn-js expects { id, bpmn20Xml }
        return Ok(new { id = def.Id.ToString(), bpmn20Xml = def.BpmnXml });
    }

    /// <summary>
    /// Updates the BPMN XML for a process definition (bpmn-js save).
    /// </summary>
    [HttpPut("{id}/xml")]
    public async Task<IActionResult> UpdateXml(Guid id, [FromBody] UpdateXmlRequest request)
    {
        return StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
        {
            Title = "BPMN XML update is unavailable",
            Detail = "The repository contract does not provide a durable XML update operation."
        });
    }

    public record UpdateXmlRequest(string BpmnXml);

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");

    private bool CanAccessTenant(string? tenantId) =>
        User.IsInRole("Admin") || string.Equals(User.FindFirstValue("tenant_id"), tenantId, StringComparison.Ordinal);

    private static ProcessDefinitionDto ToDto(CoreDef d) => new()
    {
        Id = d.Id.ToString(),
        Key = d.Key,
        Name = d.Name,
        Version = d.Version,
        TenantId = d.TenantId ?? string.Empty,
        // ...mapping für weitere Felder nach Camunda-DTO...
    };
}
