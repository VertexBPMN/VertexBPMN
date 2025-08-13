using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Core.Services;

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
