using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using VertexBPMN.Domain.Entities.Debugging;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Api.Debug;
internal class VisualDebuggingService : IVisualDebuggingService
{
	private readonly ILogger<VisualDebuggingService> _logger;
	private readonly IServiceProvider _serviceProvider;
	private readonly IHubContext<DebugHub> _hubContext;
	private readonly ConcurrentDictionary<Guid, DebugSession> _activeSessions = new();
	private readonly ConcurrentDictionary<Guid, List<Breakpoint>> _breakpoints = new();
	private readonly ConcurrentDictionary<Guid, ExecutionTrace> _executionTraces = new();
	public VisualDebuggingService(ILogger<VisualDebuggingService> logger, IServiceProvider serviceProvider, IHubContext<DebugHub> hubContext)
	{
		_logger = logger;
		_serviceProvider = serviceProvider;
		_hubContext = hubContext;
	}

	public async Task<DebugSession> StartDebuggingSessionAsync(Guid processInstanceId, DebugOptions options)
	{
		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
		{
			_logger.LogInformation("Starting debug session for process instance {ProcessInstanceId}", processInstanceId);
		}

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
		await _hubContext.Clients.Group($"process_{processInstanceId}").SendAsync("DebugSessionStarted", session);
		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
		{
			_logger.LogInformation("Debug session {SessionId} started for process {ProcessInstanceId}", session.Id, processInstanceId);
		}

		return session;
	}

	public async Task StopDebuggingSessionAsync(Guid sessionId)
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
			await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}").SendAsync("DebugSessionStopped", sessionId);
			if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
			{
				_logger.LogInformation("Debug session {SessionId} stopped", sessionId);
			}
		}
	}

	public async Task<DebugSession?> GetDebugSessionAsync(Guid sessionId)
	{
		return await Task.FromResult(_activeSessions.GetValueOrDefault(sessionId));
	}

	public async Task SetBreakpointAsync(Guid sessionId, string activityId, BreakpointCondition? condition = null)
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
		await AddTraceEventAsync(sessionId, new TraceEvent { Type = "BreakpointSet", ActivityId = activityId, Timestamp = DateTime.UtcNow, Details = JsonSerializer.Serialize(breakpoint) });
		// Notify connected clients
		await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}").SendAsync("BreakpointSet", breakpoint);
		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
		{
			_logger.LogDebug("Breakpoint set at activity {ActivityId} in session {SessionId}", activityId, sessionId);
		}
	}

	public async Task RemoveBreakpointAsync(Guid sessionId, string activityId)
	{
		if (_breakpoints.TryGetValue(sessionId, out var breakpoints))
		{
			var removed = breakpoints.RemoveAll(b => b.ActivityId == activityId);
			if (removed > 0)
			{
				// Record trace event
				await AddTraceEventAsync(sessionId, new TraceEvent { Type = "BreakpointRemoved", ActivityId = activityId, Timestamp = DateTime.UtcNow, Details = $"Removed {removed} breakpoint(s)" });
				// Notify connected clients
				if (_activeSessions.TryGetValue(sessionId, out var session))
				{
					await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}").SendAsync("BreakpointRemoved", activityId);
				}

				if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug))
				{
					_logger.LogDebug("Removed {Count} breakpoint(s) at activity {ActivityId} in session {SessionId}", removed, activityId, sessionId);
				}
			}
		}
	}

	public async Task<StepResult> StepOverAsync(Guid sessionId)
	{
		if (!_activeSessions.TryGetValue(sessionId, out var session))
		{
			throw new InvalidOperationException($"Debug session {sessionId} not found");
		}

		var result = new StepResult
		{
			Type = StepType.StepOver,
			Timestamp = DateTime.UtcNow
		};
		using var scope = _serviceProvider.CreateScope();
		var persistedStep = await scope.ServiceProvider.GetRequiredService<IVisualDebugStepService>().StepAsync(session.ProcessInstanceId);
		session.CurrentActivityId = persistedStep.EndActivityId;
		session.ExecutionState = persistedStep.ProcessCompleted ? ExecutionState.Completed : ExecutionState.Paused;
		result.StartActivity = persistedStep.StartActivityId;
		result.EndActivity = persistedStep.EndActivityId;
		result.Variables = persistedStep.Instance.Variables;
		// Check for breakpoints
		if (await CheckBreakpointHitAsync(sessionId, persistedStep.EndActivityId))
		{
			session.ExecutionState = ExecutionState.Paused;
			result.BreakpointHit = true;
			await PauseExecutionAsync(session);
		}

		// Record trace event
		await AddTraceEventAsync(sessionId, new TraceEvent { Type = "StepOver", ActivityId = persistedStep.EndActivityId, Timestamp = DateTime.UtcNow, Details = JsonSerializer.Serialize(result) });
		// Notify connected clients
		await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}").SendAsync("StepCompleted", result);
		return result;
	}

	public async Task<StepResult> StepIntoAsync(Guid sessionId)
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
		var stepResult = await ExecuteStepIntoAsync();
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
		await AddTraceEventAsync(sessionId, new TraceEvent { Type = "StepInto", ActivityId = stepResult.nextActivity, Timestamp = DateTime.UtcNow, Details = JsonSerializer.Serialize(result) });
		// Notify connected clients
		await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}").SendAsync("StepCompleted", result);
		return result;
	}

	public async Task<StepResult> StepOutAsync(Guid sessionId)
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
		var stepResult = await ExecuteStepOutAsync(session.CallStack);
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
		await AddTraceEventAsync(sessionId, new TraceEvent { Type = "StepOut", ActivityId = stepResult.nextActivity, Timestamp = DateTime.UtcNow, Details = JsonSerializer.Serialize(result) });
		// Notify connected clients
		await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}").SendAsync("StepCompleted", result);
		return result;
	}

	public async Task<ContinueResult> ContinueExecutionAsync(Guid sessionId)
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
		var continueResult = await ContinueUntilBreakpointOrCompletionAsync(session);
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
		await AddTraceEventAsync(sessionId, new TraceEvent { Type = "Continue", ActivityId = continueResult.endActivity, Timestamp = DateTime.UtcNow, Details = JsonSerializer.Serialize(result) });
		// Notify connected clients
		await _hubContext.Clients.Group($"process_{session.ProcessInstanceId}").SendAsync("ContinueCompleted", result);
		return result;
	}

	public async Task<ProcessVisualization> GetProcessVisualizationAsync(Guid processInstanceId)
	{
		using var scope = _serviceProvider.CreateScope();
		return await scope.ServiceProvider.GetRequiredService<IProcessVisualizationService>().GetAsync(processInstanceId);
	}

	public async Task<VariableInspection> InspectVariablesAsync(Guid sessionId)
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
			GlobalVariables = variables.ToDictionary(v => v.Key, v => CreateVariableDetail(v.Key, v.Value), StringComparer.Ordinal),
			LocalVariables = localVariables.ToDictionary(v => v.Key, v => CreateVariableDetail(v.Key, v.Value), StringComparer.Ordinal),
			VariableHistory = await GetVariableHistoryAsync(session.ProcessInstanceId, scope)
		};
		return inspection;
	}

	public async Task<ExecutionTrace> GetExecutionTraceAsync(Guid processInstanceId)
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

	// Helper methods
	private async Task PauseExecutionAsync(DebugSession session)
	{
		await using var scope = _serviceProvider.CreateAsyncScope();
		var runtime = scope.ServiceProvider.GetRequiredService<IRuntimeService>();
		var instance = await runtime.GetByIdAsync(session.ProcessInstanceId)
			?? throw new KeyNotFoundException($"Process instance '{session.ProcessInstanceId}' was not found.");
		if (instance.Status is VertexBPMN.Domain.Entities.ProcessInstanceStatus.Completed or VertexBPMN.Domain.Entities.ProcessInstanceStatus.Terminated)
		{
			session.ExecutionState = ExecutionState.Completed;
			return;
		}

		if (instance.Status != VertexBPMN.Domain.Entities.ProcessInstanceStatus.Suspended)
		{
			await runtime.SuspendAsync(session.ProcessInstanceId);
		}

		session.ExecutionState = ExecutionState.Paused;
	}

	private async Task ResumeExecutionAsync(DebugSession session)
	{
		await using var scope = _serviceProvider.CreateAsyncScope();
		var runtime = scope.ServiceProvider.GetRequiredService<IRuntimeService>();
		var instance = await runtime.GetByIdAsync(session.ProcessInstanceId)
			?? throw new KeyNotFoundException($"Process instance '{session.ProcessInstanceId}' was not found.");
		if (instance.Status is VertexBPMN.Domain.Entities.ProcessInstanceStatus.Completed or VertexBPMN.Domain.Entities.ProcessInstanceStatus.Terminated)
		{
			session.ExecutionState = ExecutionState.Completed;
			return;
		}

		if (instance.Status == VertexBPMN.Domain.Entities.ProcessInstanceStatus.Suspended)
		{
			await runtime.ResumeAsync(session.ProcessInstanceId);
		}

		session.ExecutionState = ExecutionState.Running;
	}

	private async Task<bool> CheckBreakpointHitAsync(Guid sessionId, string activityId)
	{
		if (!_breakpoints.TryGetValue(sessionId, out var breakpoints))
		{
			return false;
		}

		var hitBreakpoint = breakpoints.FirstOrDefault(b => b.ActivityId == activityId && b.IsEnabled);
		if (hitBreakpoint == null)
		{
			return false;
		}

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
		{
			return false;
		}

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
		{
			return 0;
		}

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

	private static VariableDetail CreateVariableDetail(string name, object? value)
	{
		return new VariableDetail
		{
			Name = name,
			Value = value?.ToString() ?? "null",
			Type = value?.GetType().Name ?? "null",
			IsExpandable = value?.GetType().IsClass == true && value is not string,
			LastModified = DateTime.UtcNow
		};
	}

	// Persisted runtime state used for inspection and trace recovery.
	private static async Task<ProcessInstanceInfo> GetProcessInstanceAsync(Guid processInstanceId, IServiceScope scope)
	{
		var runtime = scope.ServiceProvider.GetRequiredService<IRuntimeService>();
		var instance = await runtime.GetByIdAsync(processInstanceId)
			?? throw new KeyNotFoundException($"Process instance '{processInstanceId}' was not found.");
		var definition = await scope.ServiceProvider.GetRequiredService<IRepositoryService>().GetByIdAsync(instance.ProcessDefinitionId)
			?? throw new KeyNotFoundException($"Process definition '{instance.ProcessDefinitionId}' was not found.");
		return new ProcessInstanceInfo
		{
			Id = processInstanceId,
			ProcessDefinitionKey = definition.Key,
			Status = instance.Status.ToString()
		};
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "S3267", Justification = "Execution tokens are an IAsyncEnumerable; this loop stops at the first active token without materializing the stream.")]
	private static async Task<string> GetCurrentActivityAsync(Guid processInstanceId, IServiceScope scope)
	{
		var repository = scope.ServiceProvider.GetRequiredService<IExecutionTokenRepository>();
		await foreach (var token in repository.ListByProcessInstanceAsync(processInstanceId))
		{
			if (!string.Equals(token.State, "Completed", StringComparison.OrdinalIgnoreCase))
			{
				return token.CurrentNodeId;
			}
		}

		return string.Empty;
	}

	private static async Task<Dictionary<string, object>> GetVariablesSnapshotAsync(Guid processInstanceId, IServiceScope scope)
	{
		var variables = await scope.ServiceProvider.GetRequiredService<IRuntimeService>().GetVariablesAsync(processInstanceId)
			?? throw new KeyNotFoundException($"Process instance '{processInstanceId}' was not found.");
		return new Dictionary<string, object>(variables, StringComparer.Ordinal);
	}

	private static async Task<List<CallStackFrame>> BuildCallStackAsync(Guid processInstanceId, IServiceScope scope)
	{
		var runtime = scope.ServiceProvider.GetRequiredService<IRuntimeService>();
		var frames = new List<CallStackFrame>();
		var visited = new HashSet<Guid>();
		Guid? currentId = processInstanceId;
		while (currentId is { } id)
		{
			if (!visited.Add(id))
			{
				throw new InvalidOperationException("The persisted process call hierarchy contains a cycle.");
			}

			var instance = await runtime.GetByIdAsync(id)
				?? throw new KeyNotFoundException($"Process instance '{id}' was not found.");
			var activityId = await GetCurrentActivityAsync(id, scope);
			frames.Add(new CallStackFrame { ActivityId = activityId, ActivityName = activityId });
			currentId = instance.ParentProcessInstanceId;
		}

		frames.Reverse();
		for (var level = 0; level < frames.Count; level++)
		{
			frames[level].Level = level;
		}

		return frames;
	}

	// These legacy simulated stepping helpers do not execute engine operations.
	// Removing unused parameters does not constitute engine-debugger acceptance.
	private static async Task<(string nextActivity, List<CallStackFrame> callStack)> ExecuteStepIntoAsync()
	{
		await Task.Delay(10);
		return ("subprocess_activity_1", new List<CallStackFrame> { new() { ActivityId = "activity_1", ActivityName = "Parent Activity", Level = 0 }, new() { ActivityId = "subprocess_activity_1", ActivityName = "Subprocess Activity", Level = 1 } });
	}

	private static async Task<(string nextActivity, List<CallStackFrame> callStack)> ExecuteStepOutAsync(List<CallStackFrame> callStack)
	{
		await Task.Delay(10);
		var parentFrame = callStack.FirstOrDefault(f => f.Level == 0);
		return (parentFrame?.ActivityId ?? "activity_2", callStack.Where(f => f.Level == 0).ToList());
	}

	private async Task<(string endActivity, bool breakpointHit, bool processCompleted)> ContinueUntilBreakpointOrCompletionAsync(DebugSession session)
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

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "S3267", Justification = "Activity filtering is performed on a cancellable asynchronous repository stream, not an Enumerable sequence.")]
	private static async Task<Dictionary<string, object>> GetLocalVariablesAsync(Guid processInstanceId, string activityId, IServiceScope scope)
	{
		var repository = scope.ServiceProvider.GetRequiredService<IExecutionTokenRepository>();
		var variables = new Dictionary<string, object>(StringComparer.Ordinal);
		await foreach (var token in repository.ListByProcessInstanceAsync(processInstanceId))
		{
			if (!string.Equals(token.CurrentNodeId, activityId, StringComparison.Ordinal))
			{
				continue;
			}

			foreach (var entry in token.Variables)
			{
				if (!variables.TryAdd(entry.Key, entry.Value))
				{
					throw new InvalidOperationException("Multiple execution tokens have ambiguous local variables at the current activity.");
				}
			}
		}

		return variables;
	}

	private static async Task<List<VariableHistoryEntry>> GetVariableHistoryAsync(Guid processInstanceId, IServiceScope scope)
	{
		var trace = await LoadExecutionTraceAsync(processInstanceId, scope);
		var history = new List<VariableHistoryEntry>();
		Dictionary<string, object>? previous = null;
		// Only report changes between recorded step-over snapshots. Runtime history
		// without variable snapshots cannot establish a variable's previous value.
		foreach (var traceEvent in trace.Events.Where(value => value.Type == "StepOver" && value.Details is not null).OrderBy(value => value.Timestamp))
		{
			var step = JsonSerializer.Deserialize<StepResult>(traceEvent.Details!)
				?? throw new JsonException("The recorded debug step is empty.");
			if (previous is not null)
			{
				AddVariableChanges(history, previous, step.Variables, traceEvent);
			}

			previous = step.Variables;
		}

		return history;
	}

	private static void AddVariableChanges(List<VariableHistoryEntry> history, Dictionary<string, object> previous, Dictionary<string, object> current, TraceEvent traceEvent)
	{
		foreach (var name in previous.Keys.Union(current.Keys, StringComparer.Ordinal))
		{
			var hadPrevious = previous.TryGetValue(name, out var oldValue);
			var hasCurrent = current.TryGetValue(name, out var newValue);
			var oldJson = hadPrevious ? JsonSerializer.Serialize(oldValue) : null;
			var newJson = hasCurrent ? JsonSerializer.Serialize(newValue) : null;
			if (hadPrevious != hasCurrent || !string.Equals(oldJson, newJson, StringComparison.Ordinal))
			{
				history.Add(new VariableHistoryEntry { VariableName = name, OldValue = oldJson, NewValue = newJson, ChangedAt = traceEvent.Timestamp, ActivityId = traceEvent.ActivityId ?? string.Empty });
			}
		}
	}

	private static async Task<ExecutionTrace> LoadExecutionTraceAsync(Guid processInstanceId, IServiceScope scope)
	{
		var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
		var instance = await db.ProcessInstances.AsNoTracking().SingleOrDefaultAsync(value => value.Id == processInstanceId)
			?? throw new KeyNotFoundException($"Process instance '{processInstanceId}' was not found.");
		var recorded = await db.HistoryEvents.AsNoTracking()
			.Where(value => value.ProcessInstanceId == processInstanceId && value.EventType == "DEBUG_EXECUTION_TRACE")
			.OrderByDescending(value => value.Timestamp).FirstOrDefaultAsync();
		if (recorded is not null)
		{
			var trace = JsonSerializer.Deserialize<ExecutionTrace>(recorded.Data ?? string.Empty)
				?? throw new JsonException("The persisted debug trace is empty.");
			if (trace.ProcessInstanceId != processInstanceId)
			{
				throw new InvalidOperationException("The persisted debug trace references a different process instance.");
			}

			return trace;
		}

		var events = await db.HistoryEvents.AsNoTracking()
			.Where(value => value.ProcessInstanceId == processInstanceId)
			.OrderBy(value => value.Timestamp)
			.Select(value => new TraceEvent { Type = value.EventType, ActivityId = value.ElementId, Timestamp = value.Timestamp, Details = value.Details })
			.ToListAsync();
		return new ExecutionTrace
		{
			ProcessInstanceId = processInstanceId,
			StartedAt = instance.StartedAt,
			EndedAt = instance.EndedAt,
			Events = events,
			PerformanceMetrics = new PerformanceMetrics { TotalEvents = events.Count }
		};
	}

	private async Task StoreExecutionTraceAsync(ExecutionTrace trace)
	{
		await using var scope = _serviceProvider.CreateAsyncScope();
		var db = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();
		var instance = await db.ProcessInstances.AsNoTracking().SingleOrDefaultAsync(value => value.Id == trace.ProcessInstanceId)
			?? throw new KeyNotFoundException($"Process instance '{trace.ProcessInstanceId}' was not found.");
		var record = await db.HistoryEvents.FindAsync(trace.SessionId);
		if (record is not null && (record.ProcessInstanceId != trace.ProcessInstanceId || record.EventType != "DEBUG_EXECUTION_TRACE"))
		{
			throw new InvalidOperationException("The debug trace identifier conflicts with another history event.");
		}

		if (record is null)
		{
			record = new VertexBPMN.Domain.Entities.HistoryEvent
			{
				Id = trace.SessionId,
				ProcessInstanceId = trace.ProcessInstanceId,
				EventType = "DEBUG_EXECUTION_TRACE",
				TenantId = instance.TenantId
			};
			db.HistoryEvents.Add(record);
		}

		record.Timestamp = trace.EndedAt ?? DateTime.UtcNow;
		record.Data = JsonSerializer.Serialize(trace);
		await db.SaveChangesAsync();
	}
}
