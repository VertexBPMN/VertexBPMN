using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Repositories;

/// <summary>
/// Repository for managing variables.
/// </summary>
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

public interface IVariableRepository
{
    /// <summary>
    /// Adds or updates a variable.
    /// </summary>
    ValueTask UpsertAsync(Variable variable, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a variable by its unique ID.
    /// </summary>
    ValueTask<Variable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all variables for a given scope (process instance or task).
    /// </summary>
    IAsyncEnumerable<Variable> ListByScopeAsync(Guid scopeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a variable by ID.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
