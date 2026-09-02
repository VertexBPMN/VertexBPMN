using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public interface IIncidentService
{
    IAsyncEnumerable<Incident> ListAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    ValueTask ResolveAsync(Guid incidentId, string? tenantId, string? idempotencyKey = null, CancellationToken cancellationToken = default);
}
