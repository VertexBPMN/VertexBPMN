namespace VertexBPMN.Core.Services;

/// <summary>
/// Provides operations for managing user tasks, assignments, and completion.
/// </summary>
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreTask = VertexBPMN.Core.Domain.Task;

public interface ITaskService
{
    /// <summary>
    /// Claims a user task for a specific user.
    /// </summary>
    ValueTask ClaimAsync(Guid taskId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a user task with optional variables.
    /// </summary>
    ValueTask CompleteAsync(Guid taskId, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delegates a user task to another user.
    /// </summary>
    ValueTask DelegateAsync(Guid taskId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user task by ID.
    /// </summary>
    ValueTask<CoreTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all user tasks for a process instance or assignee.
    /// </summary>
    IAsyncEnumerable<CoreTask> ListAsync(Guid? processInstanceId = null, string? assignee = null, CancellationToken cancellationToken = default);
}
