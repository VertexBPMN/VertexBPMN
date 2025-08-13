using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/inspector")]
public class InspectorController : ControllerBase
{
    private readonly IRuntimeService _runtimeService;

    public InspectorController(IRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    /// <summary>
    /// Returns the current state (active tokens, activities, variables) of a process instance for live inspection/visualization.
    /// </summary>
    [HttpGet("process-instance/{id}/state")]
    public IActionResult GetProcessInstanceState(Guid id)
    {
        if (!VertexBPMN.Core.Features.FeatureFlags.LiveInspector)
            return StatusCode(503, "Live Inspector feature is disabled.");
        // Demo: Simuliertes Modell, TODO: Echte Engine-State-API
        var state = new
        {
            ProcessInstanceId = id,
            ActiveTokens = new[] { new { ActivityId = "UserTask_1", Type = "userTask" } },
            Variables = new { approved = true, amount = 1000 }
        };
        return Ok(state);
    }
}
