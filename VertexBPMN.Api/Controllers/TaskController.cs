using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using CoreTask = VertexBPMN.Core.Domain.Task;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TaskController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TaskController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public IAsyncEnumerable<CoreTask> List([FromQuery] Guid? processInstanceId = null, [FromQuery] string? assignee = null)
        => _taskService.ListAsync(processInstanceId, assignee);

    [HttpGet("{id}")]
    public async Task<ActionResult<CoreTask>> GetById(Guid id)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task is null) return NotFound();
        return task;
    }

    [HttpPost("{id}/claim")]
    public async Task<IActionResult> Claim(Guid id, [FromBody] ClaimRequest request)
    {
        await _taskService.ClaimAsync(id, request.UserId);
        return NoContent();
    }

    /// <summary>
    /// Completes a user task with optional variables.
    /// </summary>
    /// <remarks>
    /// Example request:
    ///
    ///     POST /api/task/{id}/complete
    ///     {
    ///         "Variables": { "approved": true }
    ///     }
    /// </remarks>
    /// <param name="id">Task ID</param>
    /// <param name="request">Completion request</param>
    [HttpPost("{id}/complete")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteRequest request)
    {
        await _taskService.CompleteAsync(id, request.Variables);
        return NoContent();
    }

    [HttpPost("{id}/delegate")]
    public async Task<IActionResult> Delegate(Guid id, [FromBody] DelegateRequest request)
    {
        await _taskService.DelegateAsync(id, request.UserId);
        return NoContent();
    }

    public record ClaimRequest(string UserId);
    public record CompleteRequest(IDictionary<string, object>? Variables);
    public record DelegateRequest(string UserId);
}
