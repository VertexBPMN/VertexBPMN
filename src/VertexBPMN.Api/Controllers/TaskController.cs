using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Api.Controllers;
[ApiController]
[Route("api/task")]
[Authorize]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public class TaskController : ControllerBase
{
	[FromHeader(Name = "Idempotency-Key")]
	public string? IdempotencyKey { get; set; }

	private readonly ITaskService _taskService;
	public TaskController(ITaskService taskService)
	{
		_taskService = taskService;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<UserTask>>> List([FromQuery] Guid? processInstanceId = null, [FromQuery] string? assignee = null, [FromQuery] string? tenantId = null)
	{
		var effectiveTenantId = ResolveTenantId(tenantId);
		if (effectiveTenantId is null && !User.IsInRole("Admin"))
		{
			return Forbid();
		}

		var tasks = new List<UserTask>();
		await foreach (var task in _taskService.ListAsync(processInstanceId, assignee, effectiveTenantId))
		{
			tasks.Add(task);
		}

		return tasks;
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<UserTask>> GetById(Guid id)
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

		return task;
	}

	[HttpPost("{id}/claim")]
	[Authorize(Policy = "ProcessManager")]
	public async Task<IActionResult> Claim(Guid id, [FromBody] ClaimRequest request)
	{
		if (!await CanMutateTaskAsync(id, request.TenantId))
		{
			return Forbid();
		}

		await _taskService.ClaimAsync(id, request.UserId);
		return NoContent();
	}

	/// <summary>
	/// Completes a user task with optional variables.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Example request:
	///
	///     POST /api/task/{id}/complete
	///     {
	///         "Variables": { "approved": true }
	///     }
	/// </para>
	/// </remarks>
	/// <param name = "id">Task ID</param>
	/// <param name = "request">Completion request</param>
	[HttpPost("{id}/complete")]
	[Authorize(Policy = "ProcessManager")]
	[ProducesResponseType(204)]
	public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteRequest request)
	{
		if (!await CanMutateTaskAsync(id, request.TenantId))
		{
			return Forbid();
		}

		var task = await _taskService.GetByIdAsync(id);
		if (task is null)
		{
			return NotFound();
		}
		if (!User.IsInRole("Admin") && !string.IsNullOrWhiteSpace(task.Assignee)
			&& !string.Equals(task.Assignee, User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), StringComparison.Ordinal))
		{
			return Forbid();
		}
		if (_taskService is not ITaskCompletionService
			&& ((request.ExpectedRevision.HasValue && request.ExpectedRevision != task.Revision)
			|| (request.FormSchemaHash is not null && !string.Equals(request.FormSchemaHash, task.FormSchemaHash, StringComparison.Ordinal))))
		{
			return Conflict(new { code = "staleTaskSnapshot", revision = task.Revision, formSchemaHash = task.FormSchemaHash });
		}
		var validation = VertexBPMN.Domain.Forms.TaskFormValidator.Validate(task, request.Variables);
		if (!validation.IsValid)
		{
			return BadRequest(new { code = "invalidFormData", errors = validation.Errors });
		}

		var isBound = !string.IsNullOrWhiteSpace(task.FormKey) || !string.IsNullOrWhiteSpace(task.FormSchema);
		var validated = _taskService as ITaskCompletionService;
		if (validated is not null || isBound)
		{
			if (validated is null)
			{
				return StatusCode(501, new { code = "validatedCompletionUnavailable" });
			}
			return await CompleteValidatedTaskAsync(id, task, request, validated);
		}

		await _taskService.CompleteAsync(id, request.Variables, idempotencyKey: IdempotencyKey);
		return NoContent();
	}

	private async Task<IActionResult> CompleteValidatedTaskAsync(Guid id, UserTask task, CompleteRequest request, ITaskCompletionService validated)
	{
			try
			{
				var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
				await validated.CompleteValidatedAsync(id, request.Variables, IdempotencyKey,
					request.ExpectedRevision, request.FormSchemaHash,
					User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier), task.TenantId,
					User.IsInRole("Admin"), roles, HttpContext.RequestAborted);
			}
			catch (TaskCompletionConflictException)
			{
				return Conflict(new { code = "staleTaskSnapshot" });
			}
			catch (UnauthorizedAccessException)
			{
				return Forbid();
			}
			catch (ArgumentException)
			{
				return BadRequest(new { code = "invalidFormData" });
			}
			return NoContent();
	}

	[HttpPost("{id}/delegate")]
	[Authorize(Policy = "ProcessManager")]
	public async Task<IActionResult> Delegate(Guid id, [FromBody] DelegateRequest request)
	{
		if (!await CanMutateTaskAsync(id, request.TenantId))
		{
			return Forbid();
		}

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
		{
			return string.IsNullOrWhiteSpace(requestedTenantId) || string.Equals(requestedTenantId, tenantId, StringComparison.Ordinal);
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
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type ClaimRequest retained for exposed HTTP action signatures and controller activation (TaskController).")]
	public record ClaimRequest(string UserId, string? TenantId = null);
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type CompleteRequest retained for exposed HTTP action signatures and controller activation (TaskController).")]
	public record CompleteRequest(IDictionary<string, object>? Variables, string? TenantId = null, long? ExpectedRevision = null, string? FormSchemaHash = null);
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type DelegateRequest retained for exposed HTTP action signatures and controller activation (TaskController).")]
	public record DelegateRequest(string UserId, string? TenantId = null);
}
