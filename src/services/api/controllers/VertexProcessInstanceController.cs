using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Api.Dto;
using CoreInstance = VertexBPMN.Core.Domain.ProcessInstance;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/process-instance")]
[Authorize]
public class VertexProcessInstanceController : ControllerBase
{
    private readonly IRuntimeService _runtimeService;

    public VertexProcessInstanceController(IRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    [HttpGet]
    public async IAsyncEnumerable<ProcessInstanceDto> GetAll([FromQuery] Guid? processDefinitionId = null, [FromQuery] string? tenantId = null)
    {
        await foreach (var instance in _runtimeService.ListAsync(processDefinitionId, tenantId))
            yield return ToDto(instance);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProcessInstanceDto>> GetById(Guid id)
    {
        var instance = await _runtimeService.GetByIdAsync(id);
        if (instance is null) return NotFound();
        return ToDto(instance);
    }

    private static ProcessInstanceDto ToDto(CoreInstance i) => new()
    {
        Id = i.Id,
        ProcessDefinitionId = i.ProcessDefinitionId.ToString(),
        BusinessKey = i.BusinessKey ?? string.Empty,
        TenantId = i.TenantId ?? string.Empty,
        // ...mapping für weitere Felder nach Camunda-DTO...
    };
}
