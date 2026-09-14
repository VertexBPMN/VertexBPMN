using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VertexBPMN.Api.Security;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

[ApiController]
[Route("api/external-tasks")]
[Authorize(Policy = "ExternalTaskWorker")]
[EnableRateLimiting("ExternalTaskWorker")]
[RequestSizeLimit(256 * 1024)]
public sealed class ExternalTaskController(IExternalTaskLeaseService leases) : ControllerBase
{
    [HttpPost("claim")]
    [ProducesResponseType(typeof(ExternalTaskClaimResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExternalTaskClaimResponse>> Claim(
        [FromBody] ExternalTaskClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetWorker(out var worker, out var denied)) return denied;

        try
        {
            var result = await leases.ClaimAsync(
                worker!,
                new ExternalTaskClaimCommand(request.Topics ?? [], request.MaxTasks, request.LeaseSeconds),
                cancellationToken);
            return Ok(new ExternalTaskClaimResponse(result));
        }
        catch (ExternalTaskLeaseException exception)
        {
            return LeaseProblem(exception.Code);
        }
    }

    [HttpGet("{jobId:guid}")]
    public async Task<ActionResult<ExternalTaskStatus>> Get(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!TryGetWorker(out var worker, out var denied)) return denied;
        try { return Ok(await leases.GetAsync(worker!, jobId, cancellationToken)); }
        catch (ExternalTaskLeaseException exception) { return LeaseProblem(exception.Code); }
    }

    [HttpGet("{jobId:guid}/attempts")]
    public async Task<ActionResult<ExternalTaskAttemptPage>> GetAttempts(
        Guid jobId, [FromQuery] string? after = null, [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetWorker(out var worker, out var denied)) return denied;
        try { return Ok(await leases.GetAttemptsAsync(worker!, jobId, after, limit, cancellationToken)); }
        catch (ExternalTaskLeaseException exception) { return LeaseProblem(exception.Code); }
    }

    [HttpPost("{jobId:guid}/heartbeat")]
    [ProducesResponseType(typeof(ExternalTaskHeartbeatResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExternalTaskHeartbeatResult>> Heartbeat(
        Guid jobId,
        [FromBody] ExternalTaskHeartbeatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetWorker(out var worker, out var denied)) return denied;

        try
        {
            var result = await leases.HeartbeatAsync(
                worker!,
                jobId,
                new ExternalTaskHeartbeatCommand(request.LeaseId, request.LeaseGeneration, request.LeaseSeconds),
                cancellationToken);
            return Ok(result);
        }
        catch (ExternalTaskLeaseException exception)
        {
            return LeaseProblem(exception.Code);
        }
    }

    private bool TryGetWorker(out ExternalTaskWorkerContext? worker, out ActionResult denied)
    {
        if (ExternalTaskWorkerPrincipal.TryCreate(User, out worker, out var code))
        {
            denied = null!;
            return true;
        }

        denied = LeaseProblem(code);
        return false;
    }

    private ObjectResult LeaseProblem(string code)
    {
        var status = code switch
        {
            "invalid_limits" => StatusCodes.Status400BadRequest,
            "worker_forbidden" or "topic_forbidden" or "policy_revoked" => StatusCodes.Status403Forbidden,
            "external_task_not_found" => StatusCodes.Status404NotFound,
            "lease_lost" or "activity_not_waiting" or "deadline_exceeded" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
        var problem = new ProblemDetails
        {
            Status = status,
            Title = "External task operation rejected."
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return StatusCode(status, problem);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ExternalTaskClaimRequest(
        IReadOnlyCollection<string>? Topics,
        int MaxTasks = 1,
        int LeaseSeconds = 60);

    public sealed record ExternalTaskClaimResponse(IReadOnlyList<ExternalTaskLease> Jobs);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ExternalTaskHeartbeatRequest(
        Guid LeaseId,
        long LeaseGeneration,
        int LeaseSeconds = 60);
}
