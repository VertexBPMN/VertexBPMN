using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreTask = VertexBPMN.Core.Domain.Task;

namespace VertexBPMN.Core.Services;

/// <summary>
/// In-memory implementation of ITaskService for development and testing.
/// </summary>
public class TaskService : ITaskService
{
    private readonly ConcurrentDictionary<Guid, CoreTask> _tasks = new();
    private readonly IProcessMiningEventSink _eventSink;

    public TaskService(IProcessMiningEventSink eventSink)
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

    public ValueTask CompleteAsync(Guid taskId, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.CompletedAt = DateTime.UtcNow;
            _eventSink.EmitAsync(new ProcessMiningEvent {
                EventType = "TaskCompleted",
                ProcessInstanceId = task.ProcessInstanceId.ToString(),
                TaskId = task.Id.ToString(),
                UserId = task.Assignee,
                TenantId = task.TenantId,
                Timestamp = DateTimeOffset.UtcNow,
                PayloadJson = variables != null ? System.Text.Json.JsonSerializer.Serialize(variables) : null
            }, cancellationToken);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask DelegateAsync(Guid taskId, string userId, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Assignee = userId;
            _eventSink.EmitAsync(new ProcessMiningEvent {
                EventType = "TaskDelegated",
                ProcessInstanceId = task.ProcessInstanceId.ToString(),
                TaskId = task.Id.ToString(),
                UserId = userId,
                TenantId = task.TenantId,
                Timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<CoreTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_tasks.TryGetValue(id, out var task) ? task : null);

    public async IAsyncEnumerable<CoreTask> ListAsync(Guid? processInstanceId = null, string? assignee = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var task in _tasks.Values)
        {
            if ((processInstanceId == null || task.ProcessInstanceId == processInstanceId) &&
                (assignee == null || task.Assignee == assignee))
            {
                yield return task;
            }
        }
        await System.Threading.Tasks.Task.CompletedTask;
    }
}
