using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Repositories;

/// <summary>
/// Repository for managing tasks.
/// </summary>
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

public interface ITaskRepository
{
    /// <summary>
    /// Adds a new task.
    /// </summary>
    ValueTask AddAsync(VertexBPMN.Core.Domain.Task task, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a task by its unique ID.
    /// </summary>
    ValueTask<VertexBPMN.Core.Domain.Task?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all tasks for a process instance or assignee.
    /// </summary>
    IAsyncEnumerable<VertexBPMN.Core.Domain.Task> ListAsync(Guid? processInstanceId = null, string? assignee = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a task by ID.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
