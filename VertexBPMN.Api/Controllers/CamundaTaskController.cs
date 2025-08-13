using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Api.Dto;
using CoreTask = VertexBPMN.Core.Domain.Task;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
[ApiController]
[Route("api/vertex/task")]
[Authorize]
public class VertexTaskController : ControllerBase
{
    private readonly ITaskService _taskService;

    public VertexTaskController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async IAsyncEnumerable<TaskDto> GetAll([FromQuery] Guid? processInstanceId = null, [FromQuery] string? assignee = null)
    {
        await foreach (var task in _taskService.ListAsync(processInstanceId, assignee))
            yield return ToDto(task);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskDto>> GetById(Guid id)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task is null) return NotFound();
        return ToDto(task);
    }

    /// <summary>
    /// Returns the form schema for a user task (form-js compatible).
    /// </summary>
    [HttpGet("{id}/form-schema")]
    public async Task<IActionResult> GetFormSchema(Guid id)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task is null) return NotFound();
        return Ok(new { id = task.Id, formKey = task.FormKey, schema = task.FormSchema });
    }

    /// <summary>
    /// Updates the form schema for a user task (form-js save).
    /// </summary>
    [HttpPut("{id}/form-schema")]
    public async Task<IActionResult> UpdateFormSchema(Guid id, [FromBody] UpdateFormSchemaRequest request)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task is null) return NotFound();
        task.FormSchema = request.Schema;
        task.FormKey = request.FormKey;
        // In-memory update: nothing else needed for now
        return NoContent();
    }

    public record UpdateFormSchemaRequest(string? FormKey, string? Schema);

    private static TaskDto ToDto(CoreTask t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Assignee = t.Assignee ?? string.Empty,
        // ...mapping für weitere Felder nach Camunda-DTO...
        Created = t.CreatedAt,
        FormKey = t.FormKey,
        FormSchema = t.FormSchema,
        // ...
    };
}
