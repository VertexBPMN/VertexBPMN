using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Repositories;

/// <summary>
/// Repository for managing process instances.
/// </summary>
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

public interface IProcessInstanceRepository
{
    /// <summary>
    /// Adds a new process instance.
    /// </summary>
    ValueTask AddAsync(ProcessInstance instance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a process instance by its unique ID.
    /// </summary>
    ValueTask<ProcessInstance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all process instances for a process definition or tenant.
    /// </summary>
    IAsyncEnumerable<ProcessInstance> ListAsync(Guid? processDefinitionId = null, string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a process instance by ID.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
