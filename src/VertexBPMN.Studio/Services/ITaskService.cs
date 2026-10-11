using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

internal interface ITaskService
{
    Task<IEnumerable<UserTask>> GetTasksAsync(string? tenantId = null);
    Task ClaimTaskAsync(Guid taskId, string userId, string? tenantId = null);
    Task CompleteTaskAsync(Guid taskId, IDictionary<string, object?>? variables = null, string? tenantId = null);
    Task DelegateTaskAsync(Guid taskId, string userId, string? tenantId = null);
}
