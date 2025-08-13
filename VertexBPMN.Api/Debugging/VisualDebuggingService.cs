using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;

namespace VertexBPMN.Api.Debugging;

/// <summary>
/// Advanced Visual Debugging System
/// Olympic-level feature: Innovation Differentiators - Visual Debugging
/// </summary>
public interface IVisualDebuggingService
{
    Task<DebugSession> StartDebuggingSessionAsync(Guid processInstanceId, DebugOptions options);
    Task StopDebuggingSessionAsync(Guid sessionId);
    Task<DebugSession?> GetDebugSessionAsync(Guid sessionId);
    Task SetBreakpointAsync(Guid sessionId, string activityId, BreakpointCondition? condition = null);
    Task RemoveBreakpointAsync(Guid sessionId, string activityId);
    Task<StepResult> StepOverAsync(Guid sessionId);
    Task<StepResult> StepIntoAsync(Guid sessionId);
    Task<StepResult> StepOutAsync(Guid sessionId);
    Task<ContinueResult> ContinueExecutionAsync(Guid sessionId);
    Task<ProcessVisualization> GetProcessVisualizationAsync(Guid processInstanceId);
    Task<VariableInspection> InspectVariablesAsync(Guid sessionId);
    Task<ExecutionTrace> GetExecutionTraceAsync(Guid processInstanceId);
}

public class VisualDebuggingService : IVisualDebuggingService
{
    private readonly ILogger<VisualDebuggingService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<DebugHub> _hubContext;
    private readonly ConcurrentDictionary<Guid, DebugSession> _activeSessions = new();
    private readonly ConcurrentDictionary<Guid, List<Breakpoint>> _breakpoints = new();
    private readonly ConcurrentDictionary<Guid, ExecutionTrace> _executionTraces = new();

    public VisualDebuggingService(
        ILogger<VisualDebuggingService> logger,
        IServiceProvider serviceProvider,
        IHubContext<DebugHub> hubContext)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
    }

    public async Task<DebugSession> StartDebuggingSessionAsync(Guid processInstanceId, DebugOptions options)
    {
        try
        {
            _logger.LogInformation("Starting debug session for process instance {ProcessInstanceId}", processInstanceId);

            using var scope = _serviceProvider.CreateScope();
            var processInstance = await GetProcessInstanceAsync(processInstanceId, scope);
            
            var session = new DebugSession
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = processInstanceId,
                ProcessDefinitionKey = processInstance.ProcessDefinitionKey,
                StartedAt = DateTime.UtcNow,
                Status = DebugStatus.Active,
                Options = options,
                CurrentActivityId = await GetCurrentActivityAsync(processInstanceId, scope),
                Variables = await GetVariablesSnapshotAsync(processInstanceId, scope),
                CallStack = await BuildCallStackAsync(processInstanceId, scope)
            };

            _activeSessions[session.Id] = session;
            _breakpoints[session.Id] = new List<Breakpoint>();

            // Initialize execution trace
            _executionTraces[session.Id] = new ExecutionTrace
            {
                SessionId = session.Id,
                ProcessInstanceId = processInstanceId,
                StartedAt = DateTime.UtcNow,
                Events = new List<TraceEvent>(),
                PerformanceMetrics = new PerformanceMetrics()
            };

            // Pause execution if requested
            if (options.PauseOnStart)
            {
                await PauseExecutionAsync(session);
            }

            // Notify connected clients
            await _hubContext.Clients.Group($"process_{processInstanceId}")
                .SendAsync("DebugSessionStarted", session);

            _logger.LogInformation("Debug session {SessionId} started for process {ProcessInstanceId}", 
                session.Id, processInstanceId);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting debug session for process instance {ProcessInstanceId}", processInstanceId);
            throw;
        }
    }

    public async Task StopDebuggingSessionAsync(Guid sessionId)
    {
        try
        {
            if (_activeSessions.TryRemove(sessionId, out var session))
            {
                session.Status = DebugStatus.Stopped;
                session.EndedAt = DateTime.UtcNow;

                // Resume execution if paused
                if (session.ExecutionState == ExecutionState.Paused)
                {
                    await ResumeExecutionAsync(session);
                }

                // Clean up resources
                _breakpoints.TryRemove(sessionId, out _);
                
                // Finalize execution trace
                if (_executionTraces.TryGetValue(sessionId, out var trace))
                {
                    trace.EndedAt = DateTime.UtcNow;
                    await StoreExecutionTraceAsync(trace);
                }

                // Notify connected clients
                await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}")
                    .SendAsync("DebugSessionStopped", sessionId);

                _logger.LogInformation("Debug session {SessionId} stopped", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping debug session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<DebugSession?> GetDebugSessionAsync(Guid sessionId)
    {
        return await Task.FromResult(_activeSessions.GetValueOrDefault(sessionId));
    }

    public async Task SetBreakpointAsync(Guid sessionId, string activityId, BreakpointCondition? condition = null)
    {
        try
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new InvalidOperationException($"Debug session {sessionId} not found");
            }

            if (!_breakpoints.TryGetValue(sessionId, out var breakpoints))
            {
                breakpoints = new List<Breakpoint>();
                _breakpoints[sessionId] = breakpoints;
            }

            var breakpoint = new Breakpoint
            {
                Id = Guid.NewGuid(),
                ActivityId = activityId,
                Condition = condition,
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true,
                HitCount = 0
            };

            breakpoints.Add(breakpoint);

            // Record trace event
            await AddTraceEventAsync(sessionId, new TraceEvent
            {
                Type = "BreakpointSet",
                ActivityId = activityId,
                Timestamp = DateTime.UtcNow,
                Details = JsonSerializer.Serialize(breakpoint)
            });

            // Notify connected clients
            await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}")
                .SendAsync("BreakpointSet", breakpoint);

            _logger.LogDebug("Breakpoint set at activity {ActivityId} in session {SessionId}", activityId, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting breakpoint at activity {ActivityId} in session {SessionId}", activityId, sessionId);
            throw;
        }
    }

    public async Task RemoveBreakpointAsync(Guid sessionId, string activityId)
    {
        try
        {
            if (_breakpoints.TryGetValue(sessionId, out var breakpoints))
            {
                var removed = breakpoints.RemoveAll(b => b.ActivityId == activityId);
                
                if (removed > 0)
                {
                    // Record trace event
                    await AddTraceEventAsync(sessionId, new TraceEvent
                    {
                        Type = "BreakpointRemoved",
                        ActivityId = activityId,
                        Timestamp = DateTime.UtcNow,
                        Details = $"Removed {removed} breakpoint(s)"
                    });

                    // Notify connected clients
                    if (_activeSessions.TryGetValue(sessionId, out var session))
                    {
                        await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}")
                            .SendAsync("BreakpointRemoved", activityId);
                    }

                    _logger.LogDebug("Removed {Count} breakpoint(s) at activity {ActivityId} in session {SessionId}", 
                        removed, activityId, sessionId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing breakpoint at activity {ActivityId} in session {SessionId}", activityId, sessionId);
            throw;
        }
    }

    public async Task<StepResult> StepOverAsync(Guid sessionId)
    {
        try
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new InvalidOperationException($"Debug session {sessionId} not found");
            }

            var result = new StepResult
            {
                Type = StepType.StepOver,
                StartActivity = session.CurrentActivityId,
                Timestamp = DateTime.UtcNow
            };

            // Execute single step
            using var scope = _serviceProvider.CreateScope();
            var nextActivity = await ExecuteSingleStepAsync(session.ProcessInstanceId, scope);
            
            session.CurrentActivityId = nextActivity;
            result.EndActivity = nextActivity;
            result.Variables = await GetVariablesSnapshotAsync(session.ProcessInstanceId, scope);

            // Check for breakpoints
            if (await CheckBreakpointHitAsync(sessionId, nextActivity))
            {
                session.ExecutionState = ExecutionState.Paused;
                result.BreakpointHit = true;
                await PauseExecutionAsync(session);
            }

            // Record trace event
            await AddTraceEventAsync(sessionId, new TraceEvent
            {
                Type = "StepOver",
                ActivityId = nextActivity,
                Timestamp = DateTime.UtcNow,
                Details = JsonSerializer.Serialize(result)
            });

            // Notify connected clients
            await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}")
                .SendAsync("StepCompleted", result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during step over in session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<StepResult> StepIntoAsync(Guid sessionId)
    {
        try
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new InvalidOperationException($"Debug session {sessionId} not found");
            }

            var result = new StepResult
            {
                Type = StepType.StepInto,
                StartActivity = session.CurrentActivityId,
                Timestamp = DateTime.UtcNow
            };

            // For step into, we need to dive into subprocess calls
            using var scope = _serviceProvider.CreateScope();
            var stepResult = await ExecuteStepIntoAsync(session.ProcessInstanceId, session.CurrentActivityId, scope);
            
            session.CurrentActivityId = stepResult.nextActivity;
            session.CallStack = stepResult.callStack;
            result.EndActivity = stepResult.nextActivity;
            result.Variables = await GetVariablesSnapshotAsync(session.ProcessInstanceId, scope);

            // Check for breakpoints
            if (await CheckBreakpointHitAsync(sessionId, stepResult.nextActivity))
            {
                session.ExecutionState = ExecutionState.Paused;
                result.BreakpointHit = true;
                await PauseExecutionAsync(session);
            }

            // Record trace event
            await AddTraceEventAsync(sessionId, new TraceEvent
            {
                Type = "StepInto",
                ActivityId = stepResult.nextActivity,
                Timestamp = DateTime.UtcNow,
                Details = JsonSerializer.Serialize(result)
            });

            // Notify connected clients
            await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}")
                .SendAsync("StepCompleted", result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during step into in session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<StepResult> StepOutAsync(Guid sessionId)
    {
        try
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new InvalidOperationException($"Debug session {sessionId} not found");
            }

            var result = new StepResult
            {
                Type = StepType.StepOut,
                StartActivity = session.CurrentActivityId,
                Timestamp = DateTime.UtcNow
            };

            // Step out of current subprocess/call
            using var scope = _serviceProvider.CreateScope();
            var stepResult = await ExecuteStepOutAsync(session.ProcessInstanceId, session.CallStack, scope);
            
            session.CurrentActivityId = stepResult.nextActivity;
            session.CallStack = stepResult.callStack;
            result.EndActivity = stepResult.nextActivity;
            result.Variables = await GetVariablesSnapshotAsync(session.ProcessInstanceId, scope);

            // Check for breakpoints
            if (await CheckBreakpointHitAsync(sessionId, stepResult.nextActivity))
            {
                session.ExecutionState = ExecutionState.Paused;
                result.BreakpointHit = true;
                await PauseExecutionAsync(session);
            }

            // Record trace event
            await AddTraceEventAsync(sessionId, new TraceEvent
            {
                Type = "StepOut",
                ActivityId = stepResult.nextActivity,
                Timestamp = DateTime.UtcNow,
                Details = JsonSerializer.Serialize(result)
            });

            // Notify connected clients
            await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}")
                .SendAsync("StepCompleted", result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during step out in session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<ContinueResult> ContinueExecutionAsync(Guid sessionId)
    {
        try
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new InvalidOperationException($"Debug session {sessionId} not found");
            }

            var result = new ContinueResult
            {
                SessionId = sessionId,
                StartedAt = DateTime.UtcNow,
                StartActivity = session.CurrentActivityId
            };

            session.ExecutionState = ExecutionState.Running;
            await ResumeExecutionAsync(session);

            // Continue until next breakpoint or completion
            using var scope = _serviceProvider.CreateScope();
            var continueResult = await ContinueUntilBreakpointOrCompletionAsync(session, scope);
            
            session.CurrentActivityId = continueResult.endActivity;
            result.EndActivity = continueResult.endActivity;
            result.BreakpointHit = continueResult.breakpointHit;
            result.ProcessCompleted = continueResult.processCompleted;
            result.CompletedAt = DateTime.UtcNow;

            if (continueResult.breakpointHit)
            {
                session.ExecutionState = ExecutionState.Paused;
                await PauseExecutionAsync(session);
            }
            else if (continueResult.processCompleted)
            {
                session.ExecutionState = ExecutionState.Completed;
                await StopDebuggingSessionAsync(sessionId);
            }

            // Record trace event
            await AddTraceEventAsync(sessionId, new TraceEvent
            {
                Type = "Continue",
                ActivityId = continueResult.endActivity,
                Timestamp = DateTime.UtcNow,
                Details = JsonSerializer.Serialize(result)
            });

            // Notify connected clients
            await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}")
                .SendAsync("ContinueCompleted", result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during continue execution in session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<ProcessVisualization> GetProcessVisualizationAsync(Guid processInstanceId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            
            var processInstance = await GetProcessInstanceAsync(processInstanceId, scope);
            var processDefinition = await GetProcessDefinitionAsync(processInstance.ProcessDefinitionKey, scope);
            var activeTokens = await GetActiveTokensAsync(processInstanceId, scope);
            var completedActivities = await GetCompletedActivitiesAsync(processInstanceId, scope);

            var visualization = new ProcessVisualization
            {
                ProcessInstanceId = processInstanceId,
                ProcessDefinitionKey = processInstance.ProcessDefinitionKey,
                BpmnXml = processDefinition.BpmnXml,
                ActiveTokens = activeTokens.Select(t => new VisualToken
                {
                    Id = t.Id,
                    ActivityId = t.ActivityId,
                    Position = t.Position,
                    Status = t.Status
                }).ToList(),
                CompletedActivities = completedActivities.Select(a => new VisualActivity
                {
                    ActivityId = a.ActivityId,
                    Status = a.Status,
                    CompletedAt = a.CompletedAt,
                    Duration = a.Duration,
                    ExecutionCount = a.ExecutionCount
                }).ToList(),
                Metrics = await CalculateVisualizationMetricsAsync(processInstanceId, scope)
            };

            return visualization;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process visualization for {ProcessInstanceId}", processInstanceId);
            throw;
        }
    }

    public async Task<VariableInspection> InspectVariablesAsync(Guid sessionId)
    {
        try
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                throw new InvalidOperationException($"Debug session {sessionId} not found");
            }

            using var scope = _serviceProvider.CreateScope();
            var variables = await GetVariablesSnapshotAsync(session.ProcessInstanceId, scope);
            var localVariables = await GetLocalVariablesAsync(session.ProcessInstanceId, session.CurrentActivityId, scope);

            var inspection = new VariableInspection
            {
                SessionId = sessionId,
                ProcessInstanceId = session.ProcessInstanceId,
                CurrentActivityId = session.CurrentActivityId,
                Timestamp = DateTime.UtcNow,
                GlobalVariables = variables.ToDictionary(v => v.Key, v => CreateVariableDetail(v.Key, v.Value)),
                LocalVariables = localVariables.ToDictionary(v => v.Key, v => CreateVariableDetail(v.Key, v.Value)),
                VariableHistory = await GetVariableHistoryAsync(session.ProcessInstanceId, scope)
            };

            return inspection;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inspecting variables for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<ExecutionTrace> GetExecutionTraceAsync(Guid processInstanceId)
    {
        try
        {
            // Check if we have an active session trace
            var activeTrace = _executionTraces.Values.FirstOrDefault(t => t.ProcessInstanceId == processInstanceId);
            if (activeTrace != null)
            {
                return activeTrace;
            }

            // Load from storage
            using var scope = _serviceProvider.CreateScope();
            return await LoadExecutionTraceAsync(processInstanceId, scope);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting execution trace for process instance {ProcessInstanceId}", processInstanceId);
            throw;
        }
    }

    // Helper methods
    private async Task PauseExecutionAsync(DebugSession session)
    {
        session.ExecutionState = ExecutionState.Paused;
        // Implementation would interact with the process execution engine
        await Task.Delay(1);
    }

    private async Task ResumeExecutionAsync(DebugSession session)
    {
        session.ExecutionState = ExecutionState.Running;
        // Implementation would interact with the process execution engine
        await Task.Delay(1);
    }

    private async Task<bool> CheckBreakpointHitAsync(Guid sessionId, string activityId)
    {
        if (!_breakpoints.TryGetValue(sessionId, out var breakpoints))
            return false;

        var hitBreakpoint = breakpoints.FirstOrDefault(b => b.ActivityId == activityId && b.IsEnabled);
        if (hitBreakpoint == null)
            return false;

        hitBreakpoint.HitCount++;
        hitBreakpoint.LastHitAt = DateTime.UtcNow;

        // Evaluate condition if present
        if (hitBreakpoint.Condition != null)
        {
            return await EvaluateBreakpointConditionAsync(sessionId, hitBreakpoint.Condition);
        }

        return true;
    }

    private async Task<bool> EvaluateBreakpointConditionAsync(Guid sessionId, BreakpointCondition condition)
    {
        // Simple condition evaluation - in real implementation would be more sophisticated
        if (!_activeSessions.TryGetValue(sessionId, out var session))
            return false;

        using var scope = _serviceProvider.CreateScope();
        var variables = await GetVariablesSnapshotAsync(session.ProcessInstanceId, scope);

        if (condition.VariableName != null && variables.TryGetValue(condition.VariableName, out var value))
        {
            return condition.Operator switch
            {
                "equals" => value?.ToString() == condition.Value,
                "not_equals" => value?.ToString() != condition.Value,
                "greater_than" => double.TryParse(value?.ToString(), out var d1) && double.TryParse(condition.Value, out var d2) && d1 > d2,
                "less_than" => double.TryParse(value?.ToString(), out var d3) && double.TryParse(condition.Value, out var d4) && d3 < d4,
                _ => true
            };
        }

        return condition.HitCount <= 0 || condition.HitCount <= GetBreakpointHitCount(sessionId, condition);
    }

    private int GetBreakpointHitCount(Guid sessionId, BreakpointCondition condition)
    {
        if (!_breakpoints.TryGetValue(sessionId, out var breakpoints))
            return 0;

        return breakpoints.Where(b => b.Condition?.Expression == condition.Expression).Sum(b => b.HitCount);
    }

    private async Task AddTraceEventAsync(Guid sessionId, TraceEvent traceEvent)
    {
        if (_executionTraces.TryGetValue(sessionId, out var trace))
        {
            trace.Events.Add(traceEvent);
            
            // Update performance metrics
            trace.PerformanceMetrics.TotalEvents++;
            if (traceEvent.Duration.HasValue)
            {
                trace.PerformanceMetrics.TotalExecutionTime += traceEvent.Duration.Value;
            }
        }
    }

    private VariableDetail CreateVariableDetail(string name, object? value)
    {
        return new VariableDetail
        {
            Name = name,
            Value = value?.ToString() ?? "null",
            Type = value?.GetType().Name ?? "null",
            IsExpandable = value != null && (value.GetType().IsClass && value.GetType() != typeof(string)),
            LastModified = DateTime.UtcNow
        };
    }

    // Mock implementation methods - would be replaced with actual data access
    private async Task<ProcessInstanceInfo> GetProcessInstanceAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new ProcessInstanceInfo 
        { 
            Id = processInstanceId, 
            ProcessDefinitionKey = "SampleProcess", 
            Status = "Active" 
        };
    }

    private async Task<string> GetCurrentActivityAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return "activity_1";
    }

    private async Task<Dictionary<string, object>> GetVariablesSnapshotAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new Dictionary<string, object>
        {
            { "var1", "value1" },
            { "var2", 42 },
            { "var3", true }
        };
    }

    private async Task<List<CallStackFrame>> BuildCallStackAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new List<CallStackFrame>
        {
            new() { ActivityId = "activity_1", ActivityName = "Current Activity", Level = 0 }
        };
    }

    private async Task<string> ExecuteSingleStepAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(10);
        return "activity_2";
    }

    private async Task<(string nextActivity, List<CallStackFrame> callStack)> ExecuteStepIntoAsync(Guid processInstanceId, string currentActivity, IServiceScope scope)
    {
        await Task.Delay(10);
        return ("subprocess_activity_1", new List<CallStackFrame>
        {
            new() { ActivityId = "activity_1", ActivityName = "Parent Activity", Level = 0 },
            new() { ActivityId = "subprocess_activity_1", ActivityName = "Subprocess Activity", Level = 1 }
        });
    }

    private async Task<(string nextActivity, List<CallStackFrame> callStack)> ExecuteStepOutAsync(Guid processInstanceId, List<CallStackFrame> callStack, IServiceScope scope)
    {
        await Task.Delay(10);
        var parentFrame = callStack.FirstOrDefault(f => f.Level == 0);
        return (parentFrame?.ActivityId ?? "activity_2", callStack.Where(f => f.Level == 0).ToList());
    }

    private async Task<(string endActivity, bool breakpointHit, bool processCompleted)> ContinueUntilBreakpointOrCompletionAsync(DebugSession session, IServiceScope scope)
    {
        await Task.Delay(50); // Simulate execution time
        
        // Simulate hitting a breakpoint
        if (await CheckBreakpointHitAsync(session.Id, "activity_3"))
        {
            return ("activity_3", true, false);
        }
        
        // Simulate process completion
        return ("end_event", false, true);
    }

    private async Task<ProcessDefinitionInfo> GetProcessDefinitionAsync(string processKey, IServiceScope scope)
    {
        await Task.Delay(1);
        return new ProcessDefinitionInfo 
        { 
            Key = processKey, 
            Name = $"Process {processKey}", 
            Version = 1,
            BpmnXml = "<bpmn:definitions>...</bpmn:definitions>"
        };
    }

    private async Task<List<TokenInfo>> GetActiveTokensAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new List<TokenInfo>
        {
            new() { Id = Guid.NewGuid(), ActivityId = "activity_1", Position = "waiting", Status = "active" }
        };
    }

    private async Task<List<ActivityExecutionInfo>> GetCompletedActivitiesAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new List<ActivityExecutionInfo>
        {
            new() { ActivityId = "start_event", Status = "completed", CompletedAt = DateTime.UtcNow.AddMinutes(-5), Duration = TimeSpan.FromSeconds(1), ExecutionCount = 1 }
        };
    }

    private async Task<VisualizationMetrics> CalculateVisualizationMetricsAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new VisualizationMetrics
        {
            TotalActivities = 5,
            CompletedActivities = 1,
            ActiveActivities = 1,
            TotalDuration = TimeSpan.FromMinutes(5),
            AverageActivityDuration = TimeSpan.FromMinutes(1)
        };
    }

    private async Task<Dictionary<string, object>> GetLocalVariablesAsync(Guid processInstanceId, string activityId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new Dictionary<string, object>
        {
            { "localVar1", "localValue1" }
        };
    }

    private async Task<List<VariableHistoryEntry>> GetVariableHistoryAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new List<VariableHistoryEntry>
        {
            new() { VariableName = "var1", OldValue = "oldValue", NewValue = "value1", ChangedAt = DateTime.UtcNow.AddMinutes(-2), ActivityId = "activity_1" }
        };
    }

    private async Task<ExecutionTrace> LoadExecutionTraceAsync(Guid processInstanceId, IServiceScope scope)
    {
        await Task.Delay(1);
        return new ExecutionTrace
        {
            SessionId = Guid.NewGuid(),
            ProcessInstanceId = processInstanceId,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            Events = new List<TraceEvent>(),
            PerformanceMetrics = new PerformanceMetrics()
        };
    }

    private async Task StoreExecutionTraceAsync(ExecutionTrace trace)
    {
        await Task.Delay(1);
        // Store trace to persistent storage
    }
}

// SignalR Hub for real-time debugging
public class DebugHub : Hub
{
    public async Task JoinProcessGroup(string processInstanceId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"process_{processInstanceId}");
    }

    public async Task LeaveProcessGroup(string processInstanceId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"process_{processInstanceId}");
    }
}

// Data Models
public class DebugSession
{
    public Guid Id { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DebugStatus Status { get; set; }
    public ExecutionState ExecutionState { get; set; }
    public DebugOptions Options { get; set; } = new();
    public string CurrentActivityId { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public List<CallStackFrame> CallStack { get; set; } = new();
}

public class DebugOptions
{
    public bool PauseOnStart { get; set; } = false;
    public bool PauseOnError { get; set; } = true;
    public bool RecordVariableChanges { get; set; } = true;
    public bool EnablePerformanceMetrics { get; set; } = true;
    public List<string> WatchedVariables { get; set; } = new();
}

public enum DebugStatus
{
    Starting,
    Active,
    Paused,
    Stopped,
    Error
}

public enum ExecutionState
{
    Running,
    Paused,
    Completed,
    Error
}

public class Breakpoint
{
    public Guid Id { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public BreakpointCondition? Condition { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastHitAt { get; set; }
    public bool IsEnabled { get; set; }
    public int HitCount { get; set; }
}

public class BreakpointCondition
{
    public string? VariableName { get; set; }
    public string Operator { get; set; } = string.Empty; // equals, not_equals, greater_than, less_than
    public string Value { get; set; } = string.Empty;
    public int HitCount { get; set; } = 0; // Break on Nth hit
    public string? Expression { get; set; } // Custom expression
}

public class StepResult
{
    public StepType Type { get; set; }
    public string StartActivity { get; set; } = string.Empty;
    public string EndActivity { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool BreakpointHit { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
}

public enum StepType
{
    StepOver,
    StepInto,
    StepOut
}

public class ContinueResult
{
    public Guid SessionId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string StartActivity { get; set; } = string.Empty;
    public string EndActivity { get; set; } = string.Empty;
    public bool BreakpointHit { get; set; }
    public bool ProcessCompleted { get; set; }
}

public class ProcessVisualization
{
    public Guid ProcessInstanceId { get; set; }
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public string BpmnXml { get; set; } = string.Empty;
    public List<VisualToken> ActiveTokens { get; set; } = new();
    public List<VisualActivity> CompletedActivities { get; set; } = new();
    public VisualizationMetrics Metrics { get; set; } = new();
}

public class VisualToken
{
    public Guid Id { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class VisualActivity
{
    public string ActivityId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public int ExecutionCount { get; set; }
}

public class VisualizationMetrics
{
    public int TotalActivities { get; set; }
    public int CompletedActivities { get; set; }
    public int ActiveActivities { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan AverageActivityDuration { get; set; }
}

public class VariableInspection
{
    public Guid SessionId { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public string CurrentActivityId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, VariableDetail> GlobalVariables { get; set; } = new();
    public Dictionary<string, VariableDetail> LocalVariables { get; set; } = new();
    public List<VariableHistoryEntry> VariableHistory { get; set; } = new();
}

public class VariableDetail
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsExpandable { get; set; }
    public DateTime LastModified { get; set; }
}

public class VariableHistoryEntry
{
    public string VariableName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ActivityId { get; set; } = string.Empty;
}

public class ExecutionTrace
{
    public Guid SessionId { get; set; }
    public Guid ProcessInstanceId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public List<TraceEvent> Events { get; set; } = new();
    public PerformanceMetrics PerformanceMetrics { get; set; } = new();
}

public class TraceEvent
{
    public string Type { get; set; } = string.Empty;
    public string ActivityId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
    public TimeSpan? Duration { get; set; }
}

public class PerformanceMetrics
{
    public int TotalEvents { get; set; }
    public TimeSpan TotalExecutionTime { get; set; }
    public DateTime? FastestEventTime { get; set; }
    public DateTime? SlowestEventTime { get; set; }
}

public class CallStackFrame
{
    public string ActivityId { get; set; } = string.Empty;
    public string ActivityName { get; set; } = string.Empty;
    public int Level { get; set; }
}

// Supporting classes
public class ProcessDefinitionInfo
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string BpmnXml { get; set; } = string.Empty;
}

public class ProcessInstanceInfo
{
    public Guid Id { get; set; }
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class TokenInfo
{
    public Guid Id { get; set; }
    public string ActivityId { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class ActivityExecutionInfo
{
    public string ActivityId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public int ExecutionCount { get; set; }
}
