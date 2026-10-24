using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using VertexBPMN.Api.Dto;
using VertexBPMN.Application.PersonalTasks;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Personal task inbox: a bounded, assignee/candidate-filtered query with fixed
/// pagination and sort, object-level read authorization, and server-side claim
/// semantics. The tenant, the assignee and the concurrency fence are always derived
/// and enforced on the server; the caller never picks them.
/// </summary>
[ApiController]
[Route("api/task/inbox")]
[Authorize]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public sealed class PersonalTaskController : ControllerBase
{
	[FromHeader(Name = "Idempotency-Key")]
	public string? IdempotencyKey { get; set; }

	private readonly IPersonalTaskService _personal;
	private readonly ITaskService _taskService;

	public PersonalTaskController(IPersonalTaskService personal, ITaskService taskService)
	{
		_personal = personal;
		_taskService = taskService;
	}

	[HttpGet]
	public async Task<ActionResult<PagedResultDto<PersonalTaskDto>>> Query(
		[FromQuery] string? search = null,
		[FromQuery] int page = 1,
		[FromQuery] int pageSize = 20,
		CancellationToken ct = default)
	{
		var tenantId = ClaimTenant();
		if (tenantId is null)
		{
			return Forbid();
		}

		var result = await _personal.QueryInboxAsync(
			new TaskInboxQuery(tenantId, Subject(), Roles(), search, page, pageSize), ct);

		return new PagedResultDto<PersonalTaskDto>
		{
			Items = result.Items.Select(ToDto).ToList(),
			Total = result.Total,
			Page = result.Page,
			PageSize = result.PageSize,
			HasMore = (long)result.Page * result.PageSize < result.Total,
		};
	}

	[HttpGet("{id}")]
	public async Task<ActionResult<PersonalTaskDto>> Detail(Guid id, CancellationToken ct = default)
	{
		var tenantId = ClaimTenant();
		if (tenantId is null)
		{
			return Forbid();
		}

		var row = await _personal.GetVisibleDetailAsync(id, tenantId, Subject(), Roles(), ct);
		if (row is null)
		{
			return NotFound();
		}

		return ToDto(row);
	}

	[HttpPost("{id}/claim")]
	[Authorize(Policy = "ProcessManager")]
	public async Task<IActionResult> Claim(Guid id, CancellationToken ct = default)
	{
		var tenantId = ClaimTenant();
		if (tenantId is null)
		{
			return Forbid();
		}

		var result = await _personal.ClaimForSelfAsync(id, tenantId, Subject(), Roles(), ct);

		return result.Outcome switch
		{
			PersonalClaimOutcome.Claimed => NoContent(),
			PersonalClaimOutcome.NotFound => NotFound(),
			PersonalClaimOutcome.NotCandidate => Forbid(),
			PersonalClaimOutcome.WrongState => Conflict(new { code = "taskNotClaimable" }),
			PersonalClaimOutcome.AlreadyAssigned => Conflict(new { code = "taskAlreadyAssigned" }),
			PersonalClaimOutcome.StaleRevision => Conflict(new { code = "staleTaskSnapshot" }),
			_ => StatusCode(StatusCodes.Status500InternalServerError),
		};
	}

	[HttpPost("{id}/complete")]
	[Authorize(Policy = "ProcessManager")]
	public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteRequest request, CancellationToken ct = default)
	{
		var tenantId = ClaimTenant();
		if (tenantId is null)
		{
			return Forbid();
		}

		var task = await _taskService.GetByIdAsync(id, ct);
		if (task is null || !string.Equals(task.TenantId, tenantId, StringComparison.Ordinal))
		{
			return NotFound();
		}

		if (!IsCandidateOrAssignee(task, Subject(), Roles()))
		{
			return Forbid();
		}

		if (_taskService is not ITaskCompletionService validated)
		{
			return StatusCode(StatusCodes.Status501NotImplemented, new { code = "validatedCompletionUnavailable" });
		}

		try
		{
			await validated.CompleteValidatedAsync(
				id,
				request.Variables,
				IdempotencyKey,
				request.ExpectedRevision,
				request.FormSchemaHash,
				Subject(),
				tenantId,
				User.IsInRole("Admin"),
				Roles(),
				ct);
		}
		catch (TaskCompletionConflictException)
		{
			return Conflict(new { code = "staleTaskSnapshot" });
		}
		catch (UnauthorizedAccessException)
		{
			return Forbid();
		}

		return NoContent();
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed HTTP action signatures and controller activation.")]
	public record CompleteRequest(IDictionary<string, object>? Variables = null, long? ExpectedRevision = null, string? FormSchemaHash = null);

	private string? ClaimTenant() => User.FindFirstValue("tenant_id");

	private string Subject()
		=> User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

	private string[] Roles()
		=> User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

	private static bool IsCandidateOrAssignee(UserTask task, string userId, IReadOnlyCollection<string> roles)
		=> string.Equals(task.Assignee, userId, StringComparison.Ordinal)
			|| task.CandidateUsers.Contains(userId, StringComparer.Ordinal)
			|| (!string.IsNullOrEmpty(task.CandidateRole) && roles.Contains(task.CandidateRole, StringComparer.Ordinal));

	private static PersonalTaskDto ToDto(TaskInboxRow row)
		=> new()
		{
			Id = row.Id,
			Name = row.Name,
			Status = PersonalTaskStatus.Of(row.Status),
			ProcessInstanceId = row.ProcessInstanceId,
			TenantId = row.TenantId,
			Created = row.CreatedAt,
			Due = row.DueDate,
			Completed = row.CompletedAt,
			FormKey = row.FormKey,
		};
}
