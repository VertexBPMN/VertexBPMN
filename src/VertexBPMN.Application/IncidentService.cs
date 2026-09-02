using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application;

/// <summary>
/// Persistent incident query and recovery service.
/// </summary>
public sealed class IncidentService(
    IIncidentRepository repository,
    IProcessExecutionRuntime executionRuntime) : IIncidentService
{
    public IAsyncEnumerable<Incident> ListAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default) =>
        repository.ListAsync(tenantId, cancellationToken);

    public Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetByIdAsync(id, cancellationToken).AsTask();

    public ValueTask ResolveAsync(
        Guid incidentId,
        string? tenantId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default) =>
        executionRuntime.RecoverIncidentAsync(incidentId, tenantId, idempotencyKey, cancellationToken);
}
