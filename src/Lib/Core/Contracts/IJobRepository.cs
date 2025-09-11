using VertexBPMN.Domain;

namespace VertexBPMN.Core.Contracts;

/// <summary>
/// Abstraction for job persistence, implemented in Persistence.
/// </summary>
public interface IJobRepository
{
    ValueTask AddAsync(Job job, CancellationToken cancellationToken = default);
    ValueTask<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Job> ListDueAsync(DateTime asOf, CancellationToken cancellationToken = default);
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
