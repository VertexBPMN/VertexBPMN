using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Api.Dto;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/message")]
[Authorize]
public class VertexMessageController : ControllerBase
{
    private readonly IRuntimeService _runtimeService;

    public VertexMessageController(IRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    [HttpPost]
    public async Task<ActionResult<MessageCorrelationResultDto>> Correlate([FromBody] CorrelateMessageRequest request)
    {
        var result = await _runtimeService.CorrelateMessageAsync(request.MessageName, request.ProcessInstanceId, request.Variables);
        return Ok(new MessageCorrelationResultDto
        {
            ResultType = result.ResultType,
            ExecutionId = result.ExecutionId,
            ProcessInstanceId = result.ProcessInstanceId,
            ProcessDefinitionId = result.ProcessDefinitionId
        });
    }

    public record CorrelateMessageRequest(string MessageName, string? ProcessInstanceId, IDictionary<string, object>? Variables);
}
