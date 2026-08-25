using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/task")]
[Authorize]
public class TaskController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TaskController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserTask>>> List(
        [FromQuery] Guid? processInstanceId = null,
        [FromQuery] string? assignee = null,
        [FromQuery] string? tenantId = null)
    {
        var effectiveTenantId = ResolveTenantId(tenantId);
        if (effectiveTenantId is null && !User.IsInRole("Admin")) return Forbid();
        var tasks = new List<UserTask>();
        await foreach (var task in _taskService.ListAsync(processInstanceId, assignee, effectiveTenantId))
            tasks.Add(task);
        return tasks;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserTask>> GetById(Guid id)
    {
        var task = await _taskService.GetByIdAsync(id);
        if (task is null) return NotFound();
        if (!CanAccessTenant(task.TenantId)) return Forbid();
        return task;
    }

    [HttpPost("{id}/claim")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Claim(Guid id, [FromBody] ClaimRequest request)
    {
        if (!await CanMutateTaskAsync(id, request.TenantId)) return Forbid();
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
    [Authorize(Policy = "ProcessManager")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteRequest request)
    {
        if (!await CanMutateTaskAsync(id, request.TenantId)) return Forbid();
        await _taskService.CompleteAsync(
            id,
            request.Variables,
            idempotencyKey: Request.Headers["Idempotency-Key"].FirstOrDefault());
        return NoContent();
    }

    [HttpPost("{id}/delegate")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<IActionResult> Delegate(Guid id, [FromBody] DelegateRequest request)
    {
        if (!await CanMutateTaskAsync(id, request.TenantId)) return Forbid();
        await _taskService.DelegateAsync(id, request.UserId);
        return NoContent();
    }

    private async Task<bool> CanMutateTaskAsync(Guid id, string? requestedTenantId)
    {
        var task = await _taskService.GetByIdAsync(id);
        return task is not null && CanAccessTenant(task.TenantId, requestedTenantId);
    }

    private bool CanAccessTenant(string? tenantId, string? requestedTenantId = null)
    {
        if (User.IsInRole("Admin"))
            return string.IsNullOrWhiteSpace(requestedTenantId)
                || string.Equals(requestedTenantId, tenantId, StringComparison.Ordinal);

        var currentTenantId = User.FindFirstValue("tenant_id");
        return !string.IsNullOrWhiteSpace(currentTenantId)
            && string.Equals(currentTenantId, tenantId, StringComparison.Ordinal);
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");

    public record ClaimRequest(string UserId, string? TenantId = null);
    public record CompleteRequest(IDictionary<string, object>? Variables, string? TenantId = null);
    public record DelegateRequest(string UserId, string? TenantId = null);
}
