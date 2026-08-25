using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

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

    ValueTask UpdateAsync(Job job, CancellationToken cancellationToken = default);

    async ValueTask<bool> TryLeaseAsync(Job job, string workerId, DateTime lockedUntil, CancellationToken cancellationToken = default)
    {
        job.State = "Executing";
        job.LockOwner = workerId;
        job.LockedUntil = lockedUntil;
        await UpdateAsync(job, cancellationToken);
        return true;
    }
}
