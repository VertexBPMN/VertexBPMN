using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
[ApiController]
[Route("api/vertex/message")]
[Authorize(Policy = "ProcessManager")]
public class VertexMessageController : ControllerBase
{
    private readonly IRuntimeService _runtimeService;

    public VertexMessageController(IRuntimeService runtimeService)
    {
        _runtimeService = runtimeService;
    }

    [HttpPost]
    public async Task<ActionResult<MessageCorrelationResultDto>> Correlate([FromBody] CorrelateMessageRequest request)
    {
        var tenantId = ResolveTenantId(request.TenantId);
        if (tenantId is null && !User.IsInRole("Admin")) return Forbid();
        var result = await _runtimeService.CorrelateMessageAsync(
            request.MessageName,
            request.ProcessInstanceId,
            request.Variables,
            tenantId: tenantId,
            idempotencyKey: Request.Headers["Idempotency-Key"].FirstOrDefault());
        return Ok(new MessageCorrelationResultDto
        {
            ResultType = result.ResultType,
            ExecutionId = result.ExecutionId,
            ProcessInstanceId = result.ProcessInstanceId,
            ProcessDefinitionId = result.ProcessDefinitionId
        });
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? (string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim())
            : User.FindFirstValue("tenant_id");

    public record CorrelateMessageRequest(string MessageName, string? ProcessInstanceId, IDictionary<string, object>? Variables, string? TenantId = null);
}
