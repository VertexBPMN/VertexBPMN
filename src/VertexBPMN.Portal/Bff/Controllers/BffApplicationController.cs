using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Portal.Bff;

namespace VertexBPMN.Portal.Bff.Controllers;

/// <summary>
/// Released application runtime surface for the portal client. The tenant, the actor
/// and the revision are always derived server-side (tenant/actor from the
/// authenticated identity, revision from the release binding). The caller can never
/// supply a tenant, assignee or revision. State-changing actions carry CSRF and an
/// idempotency key; data-source reads are gated to the narrow released-query bindings.
/// </summary>
[ApiController]
[Route("api/bff/applications")]
[Authorize]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public sealed class BffApplicationController(IBffRuntimeGateway gateway) : ControllerBase
{
    private const string UnsanctionedBinding = "bffUnsanctionedBinding";
    private const string UnsanctionedAction = "bffUnsanctionedAction";

    [HttpGet("{id}/released")]
    public async Task<ActionResult<ReleasedApplicationDto>> GetReleased(string id, CancellationToken ct)
    {
        if (ResolveScope() is not { } scope) { return Forbid(); }
        var released = await gateway.GetReleasedAsync(scope.TenantId, id, ct);
        return released is null ? NotFound() : Ok(BffStatusProjector.ProjectReleased(released));
    }

    [HttpGet("{id}/data-sources/{binding}")]
    public async Task<ActionResult<DataSourceResult>> ReadDataSource(string id, string binding, CancellationToken ct)
    {
        if (ResolveScope() is not { } scope) { return Forbid(); }
        var released = await gateway.GetReleasedAsync(scope.TenantId, id, ct);
        if (released is null) { return NotFound(); }

        try
        {
            ReleasedDataSourceResolver.ResolveSanctionedBinding(released.ContentJson, binding);
        }
        catch (BffUnsanctionedBindingException exception)
        {
            return Unsanctioned(UnsanctionedBinding, exception.Message);
        }

        var result = await gateway.ExecuteDataSourceReadAsync(scope.TenantId, id, binding, ct);
        return Ok(result);
    }

    [HttpPost("{id}/actions/start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(string id, [FromBody] StartRunRequest? request, CancellationToken ct)
    {
        if (ResolveScope() is not { } scope) { return Forbid(); }
        if (IdempotencyKey is null) { return BadRequest(CodeProblem("bffMissingIdempotencyKey", "An Idempotency-Key header is required.")); }
        var released = await gateway.GetReleasedAsync(scope.TenantId, id, ct);
        if (released is null) { return NotFound(); }

        // Only a sanctioned start-process action present verbatim in the released revision may
        // start; its processDefinitionId is server-resolved and never caller-supplied.
        string processDefinitionId;
        try
        {
            processDefinitionId = ReleasedProcessStartResolver.ResolveSanctionedStartProcessId(released.ContentJson);
        }
        catch (BffUnsanctionedActionException exception)
        {
            return Unsanctioned(UnsanctionedAction, exception.Message);
        }

        // State-changing effect is idempotent by key through the API's start contract.
        await gateway.StartProcessAsync(scope.TenantId, processDefinitionId, request?.Variables, IdempotencyKey, ct);
        return Accepted(new BffActionResult(ReleasedActions.StartProcess, null, "accepted"));
    }

    [HttpPost("{id}/actions/claim/{taskId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(string id, Guid taskId, CancellationToken ct)
    {
        if (ResolveScope() is not { } scope) { return Forbid(); }
        if (IdempotencyKey is null) { return BadRequest(CodeProblem("bffMissingIdempotencyKey", "An Idempotency-Key header is required.")); }
        await gateway.ClaimAsync(scope.TenantId, id, taskId, IdempotencyKey, ct);
        return NoContent();
    }

    [HttpPost("{id}/actions/complete/{taskId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(string id, Guid taskId, [FromBody] CompleteRunRequest? request, CancellationToken ct)
    {
        if (ResolveScope() is not { } scope) { return Forbid(); }
        if (IdempotencyKey is null) { return BadRequest(CodeProblem("bffMissingIdempotencyKey", "An Idempotency-Key header is required.")); }
        await gateway.CompleteAsync(scope.TenantId, id, taskId, IdempotencyKey, request?.Variables, ct);
        return NoContent();
    }

    [HttpGet("{id}/tasks/{taskId:guid}/status")]
    public async Task<ActionResult<TaskStatusProjection>> Status(string id, Guid taskId, CancellationToken ct)
    {
        if (ResolveScope() is not { } scope) { return Forbid(); }
        try
        {
            var state = await gateway.GetTaskStateAsync(scope.TenantId, id, taskId, ct);
            return Ok(BffStatusProjector.ProjectTaskStatus(state.TaskId, state.RawStatus, state.CandidateRole, state.Created, state.Completed));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [FromHeader(Name = "Idempotency-Key")]
    public string? IdempotencyKey { get; set; }

    private Scope? ResolveScope()
    {
        var tenantId = User.FindFirstValue("tenant_id");
        var actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        return string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(actor) ? null : new Scope(tenantId, actor);
    }

    private NotFoundObjectResult Unsanctioned(string code, string detail)
        => NotFound(CodeProblem(code, detail));

    private static ProblemDetails CodeProblem(string code, string detail)
    {
        var problem = new ProblemDetails { Detail = detail, Title = "Unsanctioned released binding." };
        problem.Extensions["code"] = code;
        return problem;
    }

    private sealed record Scope(string TenantId, string Actor);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed HTTP action signatures and controller activation.")]
    public sealed record StartRunRequest(IDictionary<string, object?>? Variables = null);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type retained for exposed HTTP action signatures and controller activation.")]
    public sealed record CompleteRunRequest(Dictionary<string, object?>? Variables = null);
}
