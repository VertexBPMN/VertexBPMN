using VertexBPMN.Core.Domain;

namespace VertexBPMN.Persistence.Repositories;

/// <summary>
/// Repository for managing history events.
/// </summary>
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

public interface IHistoryEventRepository
{
    /// <summary>
    /// Adds a new history event.
    /// </summary>
    ValueTask AddAsync(HistoryEvent historyEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a history event by its unique ID.
    /// </summary>
    ValueTask<HistoryEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all history events for a process instance.
    /// </summary>
    IAsyncEnumerable<HistoryEvent> ListByProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a history event by ID.
    /// </summary>
    ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
