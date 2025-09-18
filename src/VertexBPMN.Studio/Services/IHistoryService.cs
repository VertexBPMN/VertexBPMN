using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

public interface IHistoryService
{
    Task<IEnumerable<HistoryEvent>> GetHistoryByProcessInstanceAsync(Guid processInstanceId);
    Task<HistoryEvent?> GetHistoryEventByIdAsync(Guid id);
}