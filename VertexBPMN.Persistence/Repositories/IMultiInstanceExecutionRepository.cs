using VertexBPMN.Core.Domain;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VertexBPMN.Persistence.Repositories;

/// <summary>
/// Repository for managing multi-instance executions.
/// </summary>
public interface IMultiInstanceExecutionRepository
{
    /// <summary>
    /// Adds a new multi-instance execution.
    /// </summary>
    ValueTask AddAsync(MultiInstanceExecution execution, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a multi-instance execution by its unique ID.
    /// </summary>
    ValueTask<MultiInstanceExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all multi-instance executions for a process instance.
    /// </summary>
    IAsyncEnumerable<MultiInstanceExecution> ListByProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a multi-instance execution by ID.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
