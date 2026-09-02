using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

public interface IHistoryService
{
    Task<IEnumerable<HistoryEvent>> GetHistoryAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<HistoryEvent>> GetHistoryByProcessInstanceAsync(Guid processInstanceId);
    Task<HistoryEvent?> GetHistoryEventByIdAsync(Guid id);
}