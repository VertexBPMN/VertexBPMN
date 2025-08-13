using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreTask = VertexBPMN.Core.Domain.Task;
using VertexBPMN.Core.Services;
using VertexBPMN.Persistence.Repositories;

namespace VertexBPMN.Persistence.Services;

/// <summary>
/// Persistent implementation of ITaskService using ITaskRepository.
/// </summary>
public class TaskService : ITaskService
{
    private readonly ITaskRepository _repo;
    private readonly IProcessMiningEventSink _eventSink;
    public TaskService(ITaskRepository repo, IProcessMiningEventSink eventSink)
    {
        _repo = repo;
        _eventSink = eventSink;
    }

    public async ValueTask ClaimAsync(Guid taskId, string userId, CancellationToken cancellationToken = default)
    {
        var task = await _repo.GetByIdAsync(taskId, cancellationToken);
        if (task != null) {
            task.Assignee = userId;
            await _repo.AddAsync(task, cancellationToken); // Upsert
            await _eventSink.EmitAsync(new ProcessMiningEvent {
                EventType = "TaskClaimed",
                ProcessInstanceId = task.ProcessInstanceId.ToString(),
                TaskId = task.Id.ToString(),
                UserId = userId,
                TenantId = task.TenantId,
                Timestamp = DateTimeOffset.UtcNow,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { { "UserId", userId } })
            }, cancellationToken);
        }
    }

    public async ValueTask CompleteAsync(Guid taskId, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default)
    {
        var task = await _repo.GetByIdAsync(taskId, cancellationToken);
        if (task != null) {
            task.CompletedAt = DateTime.UtcNow;
            await _repo.AddAsync(task, cancellationToken); // Upsert
            await _eventSink.EmitAsync(new ProcessMiningEvent {
                EventType = "TaskCompleted",
                ProcessInstanceId = task.ProcessInstanceId.ToString(),
                TaskId = task.Id.ToString(),
                UserId = task.Assignee,
                TenantId = task.TenantId,
                Timestamp = DateTimeOffset.UtcNow,
                PayloadJson = variables != null ? System.Text.Json.JsonSerializer.Serialize(variables) : null
            }, cancellationToken);
        }
    }

    public async ValueTask DelegateAsync(Guid taskId, string userId, CancellationToken cancellationToken = default)
    {
        var task = await _repo.GetByIdAsync(taskId, cancellationToken);
        if (task != null) {
            task.Assignee = userId;
            await _repo.AddAsync(task, cancellationToken); // Upsert
            await _eventSink.EmitAsync(new ProcessMiningEvent {
                EventType = "TaskDelegated",
                ProcessInstanceId = task.ProcessInstanceId.ToString(),
                TaskId = task.Id.ToString(),
                UserId = userId,
                TenantId = task.TenantId,
                Timestamp = DateTimeOffset.UtcNow,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { { "UserId", userId } })
            }, cancellationToken);
        }
    }

    public ValueTask<CoreTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(id, cancellationToken);

    public IAsyncEnumerable<CoreTask> ListAsync(Guid? processInstanceId = null, string? assignee = null, CancellationToken cancellationToken = default)
        => _repo.ListAsync(processInstanceId, assignee, cancellationToken);
}
