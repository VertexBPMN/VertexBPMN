using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public interface IDebuggingService
{
    Task<JsonElement> TraceAsync(
        string bpmnXml,
        IDictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default);

    Task<JsonElement> StartSessionAsync(Guid processInstanceId, object? options = null, CancellationToken cancellationToken = default);
    Task<JsonElement> GetSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<JsonElement> StopSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<JsonElement> SetBreakpointAsync(Guid sessionId, string activityId, object? condition = null, CancellationToken cancellationToken = default);
    Task<JsonElement> RemoveBreakpointAsync(Guid sessionId, string activityId, CancellationToken cancellationToken = default);
    Task<JsonElement> StepOverAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<JsonElement> ContinueAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<JsonElement> GetProcessVisualizationAsync(Guid processInstanceId, CancellationToken cancellationToken = default);
    Task<JsonElement> GetExecutionTraceAsync(Guid processInstanceId, CancellationToken cancellationToken = default);
    Task<JsonElement> InspectVariablesAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
