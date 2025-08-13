namespace VertexBPMN.Core.Services;

/// <summary>
/// Provides access to historical data and audit logs for process and activity instances.
/// </summary>
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

public interface IHistoryService
{
    // Vertex-kompatible Historic Task API
    IAsyncEnumerable<HistoryEvent> ListHistoricTasksAsync(Guid? processInstanceId = null, CancellationToken cancellationToken = default);

    // Vorhandene Methoden
    IAsyncEnumerable<HistoryEvent> ListByProcessInstanceAsync(Guid processInstanceId, CancellationToken cancellationToken = default);
    ValueTask<HistoryEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
