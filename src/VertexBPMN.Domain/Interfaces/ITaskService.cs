using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Provides operations for managing user tasks, assignments, and completion.
/// </summary>

public interface ITaskService
{
    /// <summary>
    /// Claims a user task for a specific user.
    /// </summary>
    ValueTask ClaimAsync(Guid taskId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a user task with optional variables.
    /// </summary>
    ValueTask<ProcessMiningEvent?> CompleteAsync(Guid taskId, IDictionary<string, object>? variables = null, CancellationToken cancellationToken = default, string? idempotencyKey = null);

    /// <summary>
    /// Delegates a user task to another user.
    /// </summary>
    ValueTask<ProcessMiningEvent?> DelegateAsync(Guid taskId, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user task by ID.
    /// </summary>
    ValueTask<UserTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all user tasks for a process instance or assignee.
    /// </summary>
    IAsyncEnumerable<UserTask> ListAsync(Guid? processInstanceId = null, string? assignee = null, string? tenantId = null, CancellationToken cancellationToken = default);

    ValueTask<ProcessMiningEvent?> RejectAsync(Guid userTaskId, object rejectionReason, CancellationToken cancellationToken = default);
}
