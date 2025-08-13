using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RuntimeController : ControllerBase
{
    private readonly IRuntimeService _runtimeService;

    public RuntimeController(IRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    /// <summary>
    /// Starts a new process instance by process definition key.
    /// </summary>
    /// <remarks>
    /// Example request:
    ///
    ///     POST /api/runtime/start
    ///     {
    ///         "ProcessDefinitionKey": "Process_HelloWorld",
    ///         "Variables": { "foo": 42 },
    ///         "BusinessKey": null,
    ///         "TenantId": null
    ///     }
    /// </remarks>
    /// <param name="request">Start request</param>
    /// <returns>The started process instance</returns>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ProcessInstance), 201)]
    public async Task<ActionResult<ProcessInstance>> Start([FromBody] StartRequest request)
    {
        var instance = await _runtimeService.StartProcessByKeyAsync(request.ProcessDefinitionKey, request.Variables, request.BusinessKey, request.TenantId);
        return CreatedAtAction(nameof(GetById), new { id = instance.Id }, instance);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessInstance>> GetById(Guid id)
    {
        var instance = await _runtimeService.GetByIdAsync(id);
        if (instance is null) return NotFound();
        return instance;
    }

    [HttpGet]
    public IAsyncEnumerable<ProcessInstance> List([FromQuery] Guid? processDefinitionId = null, [FromQuery] string? tenantId = null)
        => _runtimeService.ListAsync(processDefinitionId, tenantId);

    public record StartRequest(string ProcessDefinitionKey, IDictionary<string, object>? Variables, string? BusinessKey, string? TenantId);
}
