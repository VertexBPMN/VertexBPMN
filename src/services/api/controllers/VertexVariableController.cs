using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Api.Dto;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/variable")]
[Authorize]
public class VertexVariableController : ControllerBase
{
    private readonly IRuntimeService _runtimeService;

    public VertexVariableController(IRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    [HttpGet("{processInstanceId}")]
    public async Task<ActionResult<IDictionary<string, VariableValueDto>>> GetVariables(Guid processInstanceId)
    {
        var variables = await _runtimeService.GetVariablesAsync(processInstanceId);
        if (variables == null) return NotFound();
        var result = new Dictionary<string, VariableValueDto>();
        foreach (var kv in variables)
        {
            result[kv.Key] = new VariableValueDto
            {
                Type = kv.Value?.GetType().Name ?? "Null",
                Value = kv.Value
            };
        }
        return result;
    }
}
