using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Api.Dto;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/signal")]
[Authorize]
public class VertexSignalController : ControllerBase
{
    private readonly IRuntimeService _runtimeService;

    public VertexSignalController(IRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    [HttpPost]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastSignalRequest request)
    {
        await _runtimeService.BroadcastSignalAsync(request.SignalName, request.Variables);
        return Ok();
    }

    public record BroadcastSignalRequest(string SignalName, IDictionary<string, object>? Variables);
}
