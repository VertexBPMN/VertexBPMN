using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces.Repositories;

/// <summary>
/// Repository for managing incidents.
/// </summary>

public interface IIncidentRepository
{
    /// <summary>
    /// Adds a new incident.
    /// </summary>
    ValueTask AddAsync(Incident incident, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an incident by its unique ID.
    /// </summary>
    ValueTask<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all incidents for a process instance.
    /// </summary>
    IAsyncEnumerable<Incident> ListByProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<Incident> ListAsync(string? tenantId = null, CancellationToken cancellationToken = default);

    ValueTask UpdateAsync(Incident incident, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an incident by ID.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
