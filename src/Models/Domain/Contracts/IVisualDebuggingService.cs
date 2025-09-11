using System;
using System.Threading.Tasks;
using VertexBPMN.Domain.Debugging;

namespace VertexBPMN.Domain.Contracts;

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
    Task<Debugging.ExecutionTrace> GetExecutionTraceAsync(Guid processInstanceId);
}