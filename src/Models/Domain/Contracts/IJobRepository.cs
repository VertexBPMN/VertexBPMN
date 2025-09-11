using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VertexBPMN.Domain.Contracts;

/// <summary>
/// Abstraction for job persistence, implemented in Persistence.
/// </summary>

public interface IJobRepository
{
    /// <summary>
    /// Adds a new job.
    /// </summary>
    ValueTask AddAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a job by its unique ID.
    /// </summary>
    ValueTask<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all jobs due for execution.
    /// </summary>
    IAsyncEnumerable<Job> ListDueAsync(DateTime asOf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a job by ID.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
