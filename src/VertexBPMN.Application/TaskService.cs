using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application;

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
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required", nameof(userId));

        var task = await _repo.GetByIdAsync(taskId, cancellationToken);
        if (task != null)
        {
            if (task.Status != UserTaskStatus.Pending)
                throw new InvalidOperationException($"Task is in {task.Status} state");

            task.Assignee = userId;
            task.LastModified = DateTime.UtcNow;
            task.ModifiedBy = userId;
            await _repo.AddAsync(task, cancellationToken); // Upsert
            await _eventSink.EmitAsync(new ProcessMiningEvent
            {
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

    public async ValueTask<ProcessMiningEvent> CompleteAsync(Guid taskId, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default)
    {
        var task = await _repo.GetByIdAsync(taskId, cancellationToken);
        if (task != null)
        {
            if (task.Status != UserTaskStatus.Pending && task.Status != UserTaskStatus.Delegated)
                throw new InvalidOperationException($"Task is in {task.Status} state");

            task.Status = UserTaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            await _repo.AddAsync(task, cancellationToken); // Upsert
            return await _eventSink.EmitAsync(new ProcessMiningEvent
            {
                EventType = "TaskCompleted",
                ProcessInstanceId = task.ProcessInstanceId.ToString(),
                TaskId = task.Id.ToString(),
                UserId = task.Assignee,
                TenantId = task.TenantId,
                Timestamp = DateTimeOffset.UtcNow,
                PayloadJson = variables != null ? System.Text.Json.JsonSerializer.Serialize(variables) : null
            }, cancellationToken);
        }

        return null;
    }

    public async ValueTask<ProcessMiningEvent> DelegateAsync(Guid taskId, string userId, CancellationToken cancellationToken = default)
    {
        var task = await _repo.GetByIdAsync(taskId, cancellationToken);
        if (task != null)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User id is required", nameof(userId));
            if (task.Status != UserTaskStatus.Pending)
                throw new InvalidOperationException($"Task is in {task.Status} state");

            task.Status = UserTaskStatus.Delegated;
            task.Assignee = userId;
            task.LastModified = DateTime.UtcNow;
            task.ModifiedBy = userId;
            await _repo.AddAsync(task, cancellationToken); // Upsert
            return await _eventSink.EmitAsync(new ProcessMiningEvent
            {
                EventType = "TaskDelegated",
                ProcessInstanceId = task.ProcessInstanceId.ToString(),
                TaskId = task.Id.ToString(),
                UserId = userId,
                TenantId = task.TenantId,
                Timestamp = DateTimeOffset.UtcNow,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { { "UserId", userId } })
            }, cancellationToken);
        }

        return default;
    }

    public ValueTask<UserTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repo.GetByIdAsync(id, cancellationToken);

    public IAsyncEnumerable<UserTask> ListAsync(Guid? processInstanceId = null, string? assignee = null, CancellationToken cancellationToken = default)
        => _repo.ListAsync(processInstanceId, assignee, cancellationToken);

    /// <summary>
    /// Rejects a user task and emits a corresponding process mining event.
    /// </summary>
    /// <param name="userTaskId">The task identifier.</param>
    /// <param name="rejectionReason">An object describing the rejection reason (will be serialized).</param>
    /// <returns>The emitted <see cref="ProcessMiningEvent"/> or null if task not found.</returns>
    public async ValueTask<ProcessMiningEvent> RejectAsync(Guid userTaskId, object rejectionReason, CancellationToken cancellationToken = default)
    {
  
        var task = await _repo.GetByIdAsync(userTaskId, cancellationToken);
        if (task == null)
            return null;

        if (task.Status == UserTaskStatus.Completed || task.Status == UserTaskStatus.Rejected)
            throw new InvalidOperationException($"Task is in {task.Status} state");

        task.Status = UserTaskStatus.Rejected;
        task.CompletedAt ??= DateTime.UtcNow;
        await _repo.AddAsync(task, cancellationToken); // Upsert

        var evt = await _eventSink.EmitAsync(new ProcessMiningEvent
        {
            EventType = "TaskRejected",
            ProcessInstanceId = task.ProcessInstanceId.ToString(),
            TaskId = task.Id.ToString(),
            UserId = task.Assignee,
            TenantId = task.TenantId,
            Timestamp = DateTimeOffset.UtcNow,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "Reason", rejectionReason }
            })
        }, cancellationToken);

        return evt;
    }
}
