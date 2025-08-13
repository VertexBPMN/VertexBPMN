using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Api.Dto;
using CoreDef = VertexBPMN.Core.Domain.ProcessDefinition;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/vertex/process-definition")]
public class VertexProcessDefinitionController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;

    public VertexProcessDefinitionController(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
    }

    [HttpGet]
    public async IAsyncEnumerable<ProcessDefinitionDto> GetAll([FromQuery] string? key = null, [FromQuery] string? tenantId = null)
    {
        await foreach (var def in _repositoryService.ListAsync(key, tenantId))
            yield return ToDto(def);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessDefinitionDto>> GetById(Guid id)
    {
        var def = await _repositoryService.GetByIdAsync(id);
        if (def is null) return NotFound();
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
        // bpmn-js expects { id, bpmn20Xml }
        return Ok(new { id = def.Id.ToString(), bpmn20Xml = def.BpmnXml });
    }

    /// <summary>
    /// Updates the BPMN XML for a process definition (bpmn-js save).
    /// </summary>
    [HttpPut("{id}/xml")]
    public async Task<IActionResult> UpdateXml(Guid id, [FromBody] UpdateXmlRequest request)
    {
        var def = await _repositoryService.GetByIdAsync(id);
        if (def is null) return NotFound();
        def.BpmnXml = request.BpmnXml;
        // In-memory update: nothing else needed for now
        return NoContent();
    }

    public record UpdateXmlRequest(string BpmnXml);

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
