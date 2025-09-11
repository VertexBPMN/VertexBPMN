namespace VertexBPMN.Core.Services;

/// <summary>
/// Provides methods for deploying and managing process definitions and resources.
/// </summary>
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

public interface IRepositoryService
{
    /// <summary>
    /// Deploys a new process definition to the engine.
    /// </summary>
    ValueTask<ProcessDefinition> DeployAsync(string bpmnXml, string name, string? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a process definition by ID.
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
