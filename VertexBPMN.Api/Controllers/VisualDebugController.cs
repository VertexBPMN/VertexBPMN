using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Api.Debugging;

namespace VertexBPMN.Api.Controllers;

/// <summary>
/// Visual Debugging Controller
/// Olympic-level feature: Innovation Differentiators - Visual Debugging
/// </summary>
[ApiController]
[Route("api/visual-debug")]
public class VisualDebugController : ControllerBase
{
    private readonly IVisualDebuggingService _debugService;
    private readonly ILogger<VisualDebugController> _logger;

    public VisualDebugController(
        IVisualDebuggingService debugService,
        ILogger<VisualDebugController> logger)
    {
        _debugService = debugService;
        _logger = logger;
    }

    /// <summary>
    /// Start a debugging session for a process instance
    /// </summary>
    [HttpPost("session/start/{processInstanceId}")]
    public async Task<ActionResult<DebugSession>> StartDebuggingSession(Guid processInstanceId, [FromBody] DebugOptions? options = null)
    {
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
    public async Task<ActionResult> StopDebuggingSession(Guid sessionId)
    {
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
    public async Task<ActionResult> SetBreakpoint(Guid sessionId, string activityId, [FromBody] BreakpointCondition? condition = null)
    {
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
    public async Task<ActionResult> RemoveBreakpoint(Guid sessionId, string activityId)
    {
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
    public async Task<ActionResult<StepResult>> StepOver(Guid sessionId)
    {
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
    public async Task<ActionResult<StepResult>> StepInto(Guid sessionId)
    {
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
    public async Task<ActionResult<StepResult>> StepOut(Guid sessionId)
    {
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
    public async Task<ActionResult<ContinueResult>> ContinueExecution(Guid sessionId)
    {
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
    public async Task<ActionResult<ProcessVisualization>> GetProcessVisualization(Guid processInstanceId)
    {
        try
        {
            var visualization = await _debugService.GetProcessVisualizationAsync(processInstanceId);
            return Ok(visualization);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process visualization for {ProcessInstanceId}", processInstanceId);
            return StatusCode(500, new { error = "Failed to get process visualization" });
        }
    }

    /// <summary>
    /// Inspect variables in current debug session
    /// </summary>
    [HttpGet("variables/{sessionId}")]
    public async Task<ActionResult<VariableInspection>> InspectVariables(Guid sessionId)
    {
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
