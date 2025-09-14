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
    IAsyncEnumerable<UserTask> ListAsync(Guid? processInstanceId = null, string? assignee = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a task by ID.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
