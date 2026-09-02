using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/test-runs")]
[Authorize(Policy = "ProcessManager")]
public sealed class TestRunsController(IRepositoryService repository, IRuntimeService runtime) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TestRunResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<TestRunResponse>> Start([FromBody] TestRunRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BpmnXml) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ProblemDetails { Detail = "BpmnXml and Name are required." });

        var tenantId = ResolveTenantId(request.TenantId);
        if (tenantId is null && !User.IsInRole("Admin")) return Forbid();

        var definition = await repository.DeployAsync(request.BpmnXml, request.Name, tenantId, cancellationToken);
        var instance = await runtime.StartProcessByKeyAsync(
            definition.Key,
            request.Variables,
            request.BusinessKey ?? $"test-run-{Guid.NewGuid():N}",
            tenantId,
            cancellationToken);

        return Created($"api/test-runs/{instance.Id}", new TestRunResponse(
            new TestRunDefinitionResponse(
                definition.Id.ToString(),
                definition.Key,
                definition.Name,
                definition.Version,
                definition.TenantId,
                Suspended: false),
            new TestRunInstanceResponse(
                instance.Id,
                instance.BusinessKey,
                instance.ProcessDefinitionId.ToString(),
                definition.Key,
                instance.TenantId,
                instance.State,
                instance.Variables)));
    }

    private string? ResolveTenantId(string? requestedTenantId) =>
        User.IsInRole("Admin")
            ? string.IsNullOrWhiteSpace(requestedTenantId) ? null : requestedTenantId.Trim()
            : User.FindFirstValue("tenant_id");

    public sealed record TestRunRequest(string BpmnXml, string Name, IDictionary<string, object>? Variables, string? BusinessKey, string? TenantId);
    /// <summary>
    /// Stable transport contract for SDK consumers. Domain entities deliberately do not cross the API boundary.
    /// </summary>
    public sealed record TestRunResponse(TestRunDefinitionResponse Definition, TestRunInstanceResponse Instance);
    public sealed record TestRunDefinitionResponse(string Id, string Key, string Name, int Version, string? TenantId, bool Suspended);
    public sealed record TestRunInstanceResponse(Guid Id, string? BusinessKey, string ProcessDefinitionId, string ProcessDefinitionKey, string? TenantId, string State, IDictionary<string, object> Variables);
}
