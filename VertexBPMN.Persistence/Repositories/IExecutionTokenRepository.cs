using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Repositories;

/// <summary>
/// Repository for managing execution tokens.
/// </summary>
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

public interface IExecutionTokenRepository
{
    /// <summary>
    /// Adds a new execution token.
    /// </summary>
    ValueTask AddAsync(ExecutionToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a token by its unique ID.
    /// </summary>
    ValueTask<ExecutionToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all tokens for a process instance.
    /// </summary>
    IAsyncEnumerable<ExecutionToken> ListByProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a token by ID.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
