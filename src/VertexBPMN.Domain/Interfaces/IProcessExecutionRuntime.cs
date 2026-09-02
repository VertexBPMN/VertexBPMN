using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// The single durable BPMN execution path used by repository, runtime, task and job APIs.
/// </summary>
public interface IProcessExecutionRuntime
{
    ValueTask<ProcessInstance> StartAsync(
        ProcessDefinition definition,
        IDictionary<string, object>? variables,
        string? businessKey,
        string? tenantId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    ValueTask<MessageCorrelationResult> CorrelateMessageAsync(
        string messageName,
        Guid? processInstanceId,
        IDictionary<string, object>? variables,
        string? tenantId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    ValueTask BroadcastSignalAsync(
        string signalName,
        IDictionary<string, object>? variables,
        string? tenantId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    ValueTask CompleteUserTaskAsync(
        Guid taskId,
        IDictionary<string, object>? variables,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    ValueTask<bool> ExecuteJobAsync(
        Guid jobId,
        string workerId,
        CancellationToken cancellationToken = default);

    ValueTask RecoverIncidentAsync(
        Guid incidentId,
        string? tenantId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}
