using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Entities.Debugging;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Visual Debugging Controller
/// Olympic-level feature: Innovation Differentiators - Visual Debugging
/// </summary>
[ApiController]
[Authorize]
[Route("api/visual-debug")]
public class VisualDebugController : ControllerBase
{
    private readonly IVisualDebuggingService _debugService;
    private readonly ILogger<VisualDebugController> _logger;
    private readonly IRuntimeService _runtimeService;

    public VisualDebugController(
        IVisualDebuggingService debugService,
        ILogger<VisualDebugController> logger,
        IRuntimeService runtimeService)
    {
        _debugService = debugService;
        _logger = logger;
        _runtimeService = runtimeService;
    }

    /// <summary>
    /// Start a debugging session for a process instance
    /// </summary>
    [HttpPost("session/start/{processInstanceId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<DebugSession>> StartDebuggingSession(Guid processInstanceId, [FromBody] DebugOptions? options = null)
    {
        if (!await CanAccessInstanceAsync(processInstanceId)) return NotFound();
        try
        {
            var session = await _debugService.StartDebuggingSessionAsync(processInstanceId, options ?? new DebugOptions());
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting debug session for process {ProcessInstanceId}", processInstanceId);
            return StatusCode(500, new { error = "Failed to start debugging session" });
        }
    }

    /// <summary>
    /// Stop a debugging session
    /// </summary>
    [HttpPost("session/stop/{sessionId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult> StopDebuggingSession(Guid sessionId)
    {
        if (!await CanAccessSessionAsync(sessionId)) return NotFound();
        try
        {
            await _debugService.StopDebuggingSessionAsync(sessionId);
            return Ok(new { message = "Debug session stopped successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping debug session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to stop debugging session" });
        }
    }

    /// <summary>
    /// Get debug session information
    /// </summary>
    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult<DebugSession?>> GetDebugSession(Guid sessionId)
    {
        if (!await CanAccessSessionAsync(sessionId)) return NotFound();
        try
        {
            var session = await _debugService.GetDebugSessionAsync(sessionId);
            if (session == null)
            {
                return NotFound(new { error = "Debug session not found" });
            }
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting debug session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to get debug session" });
        }
    }

    /// <summary>
    /// Set a breakpoint at an activity
    /// </summary>
    [HttpPost("breakpoint/{sessionId}/{activityId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult> SetBreakpoint(Guid sessionId, string activityId, [FromBody] BreakpointCondition? condition = null)
    {
        if (!await CanAccessSessionAsync(sessionId)) return NotFound();
        try
        {
            await _debugService.SetBreakpointAsync(sessionId, activityId, condition);
            return Ok(new { message = $"Breakpoint set at activity {activityId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting breakpoint at {ActivityId} in session {SessionId}", activityId, sessionId);
            return StatusCode(500, new { error = "Failed to set breakpoint" });
        }
    }

    /// <summary>
    /// Remove a breakpoint from an activity
    /// </summary>
    [HttpDelete("breakpoint/{sessionId}/{activityId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult> RemoveBreakpoint(Guid sessionId, string activityId)
    {
        if (!await CanAccessSessionAsync(sessionId)) return NotFound();
        try
        {
            await _debugService.RemoveBreakpointAsync(sessionId, activityId);
            return Ok(new { message = $"Breakpoint removed from activity {activityId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing breakpoint from {ActivityId} in session {SessionId}", activityId, sessionId);
            return StatusCode(500, new { error = "Failed to remove breakpoint" });
        }
    }

    /// <summary>
    /// Step over current activity
    /// </summary>
    [HttpPost("step/over/{sessionId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<StepResult>> StepOver(Guid sessionId)
    {
        if (!await CanAccessSessionAsync(sessionId)) return NotFound();
        try
        {
            var result = await _debugService.StepOverAsync(sessionId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during step over in session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to step over" });
        }
    }

    /// <summary>
    /// Step into current activity (dive into subprocesses)
    /// </summary>
    [HttpPost("step/into/{sessionId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<StepResult>> StepInto(Guid sessionId)
    {
        if (!await CanAccessSessionAsync(sessionId)) return NotFound();
        try
        {
            var result = await _debugService.StepIntoAsync(sessionId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during step into in session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to step into" });
        }
    }

    /// <summary>
    /// Step out of current subprocess
    /// </summary>
    [HttpPost("step/out/{sessionId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<StepResult>> StepOut(Guid sessionId)
    {
        if (!await CanAccessSessionAsync(sessionId)) return NotFound();
        try
        {
            var result = await _debugService.StepOutAsync(sessionId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during step out in session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to step out" });
        }
    }

    /// <summary>
    /// Continue execution until next breakpoint or completion
    /// </summary>
    [HttpPost("continue/{sessionId}")]
    [Authorize(Policy = "ProcessManager")]
    public async Task<ActionResult<ContinueResult>> ContinueExecution(Guid sessionId)
    {
        if (!await CanAccessSessionAsync(sessionId)) return NotFound();
        try
        {
            var result = await _debugService.ContinueExecutionAsync(sessionId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during continue execution in session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to continue execution" });
        }
    }

    /// <summary>
    /// Get visual representation of process with execution state
    /// </summary>
    [HttpGet("visualize/{processInstanceId}")]
    public async Task<ActionResult<ProcessVisualization>> GetProcessVisualization(Guid processInstanceId, CancellationToken cancellationToken)
    {
        try
        {
            var instance = await _runtimeService.GetByIdAsync(processInstanceId, cancellationToken);
            if (instance == null)
                return NotFound();
            if (!CanAccessTenant(instance.TenantId))
                return Forbid();

            var visualization = await _debugService.GetProcessVisualizationAsync(processInstanceId);
            return Ok(visualization);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Process visualization is not available",
                Detail = exception.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process visualization for {ProcessInstanceId}", processInstanceId);
            return StatusCode(500, new { error = "Failed to get process visualization" });
        }
    }

    private bool CanAccessTenant(string? tenantId) =>
        User.IsInRole("Admin") ||
        (!string.IsNullOrWhiteSpace(User.FindFirstValue("tenant_id")) &&
         string.Equals(User.FindFirstValue("tenant_id"), tenantId, StringComparison.Ordinal));

    private async Task<bool> CanAccessInstanceAsync(Guid id)
    {
        var instance = await _runtimeService.GetByIdAsync(id, HttpContext.RequestAborted);
        return instance is not null && CanAccessTenant(instance.TenantId);
    }

    private async Task<bool> CanAccessSessionAsync(Guid id)
    {
        var session = await _debugService.GetDebugSessionAsync(id);
        return session is not null && await CanAccessInstanceAsync(session.ProcessInstanceId);
    }

    /// <summary>
    /// Inspect variables in current debug session
    /// </summary>
    [HttpGet("variables/{sessionId}")]
    public async Task<ActionResult<VariableInspection>> InspectVariables(Guid sessionId)
    {
        if (!await CanAccessSessionAsync(sessionId)) return NotFound();
        try
        {
            var inspection = await _debugService.InspectVariablesAsync(sessionId);
            return Ok(inspection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inspecting variables for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to inspect variables" });
        }
    }

    /// <summary>
    /// Get execution trace for process instance
    /// </summary>
    [HttpGet("trace/{processInstanceId}")]
    public async Task<ActionResult<ExecutionTrace>> GetExecutionTrace(Guid processInstanceId)
    {
        if (!await CanAccessInstanceAsync(processInstanceId)) return NotFound();
        try
        {
            var trace = await _debugService.GetExecutionTraceAsync(processInstanceId);
            return Ok(trace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting execution trace for process {ProcessInstanceId}", processInstanceId);
            return StatusCode(500, new { error = "Failed to get execution trace" });
        }
    }
}
