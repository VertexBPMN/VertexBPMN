
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Repositories;

/// <summary>
/// Repository for managing process definitions.
/// </summary>
public interface IProcessDefinitionRepository
{
    /// <summary>
    /// Adds a new process definition.
    /// </summary>
    ValueTask AddAsync(ProcessDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a process definition by its unique ID.
    /// </summary>
    ValueTask<ProcessDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest process definition by key.
    /// </summary>
    ValueTask<ProcessDefinition?> GetLatestByKeyAsync(string key, string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all process definitions, optionally filtered by key or tenant.
    /// </summary>
    IAsyncEnumerable<ProcessDefinition> ListAsync(string? key = null, string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a process definition by ID.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
