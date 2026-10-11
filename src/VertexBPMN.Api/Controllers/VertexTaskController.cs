using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace VertexBPMN.Api.Controllers;
[ApiController]
[Route("api/vertex/task")]
[Authorize]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public class VertexTaskController : ControllerBase
{
	private readonly ITaskService _taskService;
	public VertexTaskController(ITaskService taskService)
	{
		_taskService = taskService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<TaskDto>>> GetAll([FromQuery] Guid? processInstanceId = null, [FromQuery] string? assignee = null, [FromQuery] string? tenantId = null)
	{
		var effectiveTenantId = ResolveTenantId(tenantId);
		if (effectiveTenantId is null && !User.IsInRole("Admin"))
		{
			return Forbid();
		}

		var tasks = new List<TaskDto>();
		await foreach (var task in _taskService.ListAsync(processInstanceId, assignee, effectiveTenantId))
		{
			tasks.Add(ToDto(task));
		}

		return tasks;
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<TaskDto>> GetById(Guid id)
	{
		var task = await _taskService.GetByIdAsync(id);
		if (task is null)
		{
			return NotFound();
		}

		if (!CanAccessTenant(task.TenantId))
		{
			return Forbid();
		}

		return ToDto(task);
	}

	/// <summary>
	/// Returns the form schema for a user task (form-js compatible).
	/// </summary>
	[HttpGet("{id}/form-schema")]
	public async Task<IActionResult> GetFormSchema(Guid id)
	{
		var task = await _taskService.GetByIdAsync(id);
		if (task is null)
		{
			return NotFound();
		}

		if (!CanAccessTenant(task.TenantId))
		{
			return Forbid();
		}

		return Ok(new { id = task.Id, formKey = task.FormKey, schema = task.FormSchema });
	}

	/// <summary>
	/// Updates the form schema for a user task (form-js save).
	/// </summary>
	[HttpPut("{id}/form-schema")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060", Justification = "Existing request contract is preserved while this unsupported persistence operation returns 501 after task authorization.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RCS1163", Justification = "The schema request is retained for HTTP compatibility; no unpersisted update is reported as successful.")]
	[Authorize(Policy = "ProcessManager")]
	public async Task<IActionResult> UpdateFormSchema(Guid id, [FromBody] UpdateFormSchemaRequest request)
	{
		var task = await _taskService.GetByIdAsync(id);
		if (task is null)
		{
			return NotFound();
		}

		if (!CanAccessTenant(task.TenantId))
		{
			return Forbid();
		}

		return Problem(detail: "Updating task form schemas requires a persistence-backed form service.", statusCode: StatusCodes.Status501NotImplemented, title: "Task form persistence is not available");
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type UpdateFormSchemaRequest retained for exposed HTTP action signatures and controller activation (VertexTaskController).")]
	public record UpdateFormSchemaRequest(string? FormKey, string? Schema);
	private bool CanAccessTenant(string? tenantId)
	{
		if (User.IsInRole("Admin"))
		{
			return true;
		}

		var currentTenantId = User.FindFirstValue("tenant_id");
		return !string.IsNullOrWhiteSpace(currentTenantId) && string.Equals(currentTenantId, tenantId, StringComparison.Ordinal);
	}

	private string? ResolveTenantId(string? requestedTenantId)
	{
		if (!User.IsInRole("Admin"))
		{
			return User.FindFirstValue("tenant_id");
		}

		return string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();
	}

	private static TaskDto ToDto(UserTask t) => new()
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
