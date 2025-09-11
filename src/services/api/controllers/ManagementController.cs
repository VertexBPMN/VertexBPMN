using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/management")]
public class ManagementController : ControllerBase
{
    private readonly IManagementService _managementService;

    public ManagementController(IManagementService managementService)
    {
        _managementService = managementService;
    }

    [HttpPost("suspend-process-instance/{id}")]
    public async Task<IActionResult> SuspendProcessInstance(Guid id)
    {
        await _managementService.SuspendProcessInstanceAsync(id);
        return NoContent();
    }

    [HttpPost("resume-process-instance/{id}")]
    public async Task<IActionResult> ResumeProcessInstance(Guid id)
    {
        await _managementService.ResumeProcessInstanceAsync(id);
        return NoContent();
    }

    [HttpPost("delete-process-instance/{id}")]
    public async Task<IActionResult> DeleteProcessInstance(Guid id)
    {
        await _managementService.DeleteProcessInstanceAsync(id);
        return NoContent();
    }
    [HttpGet("metrics")]
    public async Task<ActionResult<IDictionary<string, object>>> GetMetrics()
    {
        var metrics = await _managementService.GetMetricsAsync();
        return Ok(metrics);
    }
}
