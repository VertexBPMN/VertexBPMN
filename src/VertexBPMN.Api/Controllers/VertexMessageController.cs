using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace VertexBPMN.Api.Controllers;
[ApiController]
[Route("api/vertex/message")]
[Authorize(Policy = "ProcessManager")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public class VertexMessageController : ControllerBase
{
	[FromHeader(Name = "Idempotency-Key")]
	public string? IdempotencyKey { get; set; }

	private readonly IRuntimeService _runtimeService;
	public VertexMessageController(IRuntimeService runtimeService)
	{
		_runtimeService = runtimeService;
	}

	[HttpPost]
	public async Task<ActionResult<MessageCorrelationResultDto>> Correlate([FromBody] CorrelateMessageRequest request)
	{
		var tenantId = ResolveTenantId(request.TenantId);
		if (tenantId is null && !User.IsInRole("Admin"))
		{
			return Forbid();
		}

		var result = await _runtimeService.CorrelateMessageAsync(request.MessageName, request.ProcessInstanceId, request.Variables, tenantId: tenantId, idempotencyKey: IdempotencyKey);
		return Ok(new MessageCorrelationResultDto { ResultType = result.ResultType, ExecutionId = result.ExecutionId, ProcessInstanceId = result.ProcessInstanceId, ProcessDefinitionId = result.ProcessDefinitionId });
	}

	private string? ResolveTenantId(string? requestedTenantId)
	{
		if (!User.IsInRole("Admin"))
		{
			return User.FindFirstValue("tenant_id");
		}

		return string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim();
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type CorrelateMessageRequest retained for exposed HTTP action signatures and controller activation (VertexMessageController).")]
	public record CorrelateMessageRequest(string MessageName, string? ProcessInstanceId, IDictionary<string, object>? Variables, string? TenantId = null);
}
