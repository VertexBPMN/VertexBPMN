using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces.Repositories;

/// <summary>
/// Repository for managing tasks.
/// </summary>

public interface ITaskRepository
{
    /// <summary>
    /// Adds a new task.
    /// </summary>
    ValueTask AddAsync(UserTask userTask, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a task by its unique ID.
    /// </summary>
    ValueTask<UserTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all tasks for a process instance or assignee.
    /// </summary>
    IAsyncEnumerable<UserTask> ListAsync(Guid? processInstanceId = null, string? assignee = null, string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a task by ID.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the bounded, paginated personal-inbox query. The query already
    /// carries the tenant and the caller identity so no cross-tenant or
    /// cross-owner rows can be returned.
    /// </summary>
    ValueTask<PagedTaskInbox> QueryInboxAsync(TaskInboxQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs an atomic, server-side claim. Only the authenticated caller can
    /// become the assignee; the update is fenced on <paramref name="attempt"/>'s
    /// expected revision, open task state and current assignment.
    /// </summary>
    ValueTask<TaskClaimOutcome> TryClaimAsync(TaskClaimAttempt attempt, CancellationToken cancellationToken = default);
}
