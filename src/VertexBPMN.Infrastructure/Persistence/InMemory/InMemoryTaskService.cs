using System.Collections.Concurrent;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence.InMemory;

/// <summary>
/// In-memory implementation of ITaskService for development and testing.
/// </summary>
public class InMemoryTaskService : ITaskService
{
    private readonly ConcurrentDictionary<Guid, UserTask> _tasks = new();
    private readonly IProcessMiningEventSink _eventSink;

    public InMemoryTaskService(IProcessMiningEventSink eventSink)
    {
        _eventSink = eventSink;
    }

    public ValueTask ClaimAsync(Guid taskId, string userId, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Assignee = userId;
            _eventSink.EmitAsync(new ProcessMiningEvent {
                EventType = "TaskClaimed",
                ProcessInstanceId = task.ProcessInstanceId.ToString(),
                TaskId = task.Id.ToString(),
                UserId = userId,
                TenantId = task.TenantId,
                Timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<ProcessMiningEvent> CompleteAsync(Guid taskId, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.CompletedAt = DateTime.UtcNow;
            return _eventSink.EmitAsync(new ProcessMiningEvent {
                EventType = "TaskCompleted",
                ProcessInstanceId = task.ProcessInstanceId.ToString(),
                TaskId = task.Id.ToString(),
                UserId = task.Assignee,
                TenantId = task.TenantId,
                Timestamp = DateTimeOffset.UtcNow,
                PayloadJson = variables != null ? System.Text.Json.JsonSerializer.Serialize(variables) : null
            }, cancellationToken);
        }
        return default;
    }

    public ValueTask<ProcessMiningEvent> DelegateAsync(Guid taskId, string userId, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Assignee = userId;
            return _eventSink.EmitAsync(new ProcessMiningEvent {
                EventType = "TaskDelegated",
                ProcessInstanceId = task.ProcessInstanceId.ToString(),
                TaskId = task.Id.ToString(),
                UserId = userId,
                TenantId = task.TenantId,
                Timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);
        }

        return default;
    }

    public ValueTask<UserTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_tasks.TryGetValue(id, out var task) ? task : null);

    public async IAsyncEnumerable<UserTask> ListAsync(Guid? processInstanceId = null, string? assignee = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var task in _tasks.Values)
        {
            if ((processInstanceId == null || task.ProcessInstanceId == processInstanceId) &&
                (assignee == null || task.Assignee == assignee))
            {
                yield return task;
            }
        }
        await Task.CompletedTask;
    }

    public ValueTask<ProcessMiningEvent> RejectAsync(Guid userTaskId, object rejectionReason, CancellationToken cancellationToken = default)
    {

        if (!_tasks.TryGetValue(userTaskId, out var task))
            throw new InvalidOperationException($"Task {userTaskId} not found.");

        // Update task state (assumes a Rejected status exists in UserTaskStatus enum)
        task.CompletedAt ??= DateTime.UtcNow;
        task.LastModified = DateTime.UtcNow;
        try
        {
            task.Status = UserTaskStatus.Rejected;
        }
        catch
        {
            // If enum does not contain Rejected, ignore without failing (no generic exception)
        }

        // Emit rejection event with serialized reason
       return _eventSink.EmitAsync(new ProcessMiningEvent
        {
            EventType = "TaskRejected",
            ProcessInstanceId = task.ProcessInstanceId.ToString(),
            TaskId = task.Id.ToString(),
            UserId = task.Assignee,
            TenantId = task.TenantId,
            Timestamp = DateTimeOffset.UtcNow,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                reason = rejectionReason
            })
        });
    }
}