using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/repository")]
public class RepositoryController : ControllerBase
{
    private readonly IRepositoryService _repositoryService;

    public RepositoryController(IRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
    }

    [HttpGet]
    public IAsyncEnumerable<ProcessDefinition> GetAll([FromQuery] string? key = null, [FromQuery] string? tenantId = null)
        => _repositoryService.ListAsync(key, tenantId);

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessDefinition>> GetById(Guid id)
    {
        var def = await _repositoryService.GetByIdAsync(id);
        if (def is null) return NotFound();
        return def;
    }

    /// <summary>
    /// Deploys a new BPMN process definition.
    /// </summary>
    /// <remarks>
    /// Example request:
    ///
    ///     POST /api/repository
    ///     {
    ///         "bpmnXml": "&lt;definitions ...&gt;...&lt;/definitions&gt;",
    ///         "name": "hello-world.bpmn",
    ///         "tenantId": null
    ///     }
    /// </remarks>
    /// <param name="request">Deployment request</param>
    /// <returns>The deployed process definition</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ProcessDefinition), 201)]
    public async Task<ActionResult<ProcessDefinition>> Deploy([FromBody] RepositoryDeployRequest request)
    {
        var def = await _repositoryService.DeployAsync(request.BpmnXml, request.Name, request.TenantId);
        return CreatedAtAction(nameof(GetById), new { id = def.Id }, def);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _repositoryService.DeleteAsync(id);
        return NoContent();
    }
    public record RepositoryDeployRequest(string BpmnXml, string Name, string? TenantId);
}
