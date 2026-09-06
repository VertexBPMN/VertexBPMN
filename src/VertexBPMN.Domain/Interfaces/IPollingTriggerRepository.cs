using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public interface IPollingTriggerRepository
{
    Task AddAsync(PollingTriggerRecord trigger, CancellationToken cancellationToken = default);
    Task<PollingTriggerRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PollingTriggerRecord>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PollingTriggerRecord>> ListDueAsync(DateTime asOf, CancellationToken cancellationToken = default);
    Task<bool> TryLeaseAsync(PollingTriggerRecord trigger, string workerId, DateTime lockedUntil, CancellationToken cancellationToken = default);
    Task UpdateAsync(PollingTriggerRecord trigger, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
