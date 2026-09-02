using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

/// <summary>
/// Provides access to historical data and audit logs for process and activity instances.
/// </summary>

public interface IHistoryService
{
    // Vertex-kompatible Historic Task API
    IAsyncEnumerable<HistoryEvent> ListHistoricTasksAsync(Guid? processInstanceId = null, string? tenantId = null, CancellationToken cancellationToken = default);

    // Vorhandene Methoden
    IAsyncEnumerable<HistoryEvent> ListAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<HistoryEvent> ListByProcessInstanceAsync(Guid processInstanceId, string? tenantId = null, CancellationToken cancellationToken = default);
    ValueTask<HistoryEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
