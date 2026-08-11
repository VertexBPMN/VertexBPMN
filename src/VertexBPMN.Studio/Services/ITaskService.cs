using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

public interface ITaskService
{
    Task<IEnumerable<UserTask>> GetTasksAsync(string? tenantId = null);
    Task ClaimTaskAsync(Guid taskId, string userId, string? tenantId = null);
    Task CompleteTaskAsync(Guid taskId, IDictionary<string, object?>? variables = null, string? tenantId = null);
    Task DelegateTaskAsync(Guid taskId, string userId, string? tenantId = null);
}

public sealed class TaskService : ITaskService
{
    private readonly IBpmnEngineService _engineService;

    public TaskService(IBpmnEngineService engineService) => _engineService = engineService;

    public Task<IEnumerable<UserTask>> GetTasksAsync(string? tenantId = null) => _engineService.GetTasksAsync(tenantId);
    public Task ClaimTaskAsync(Guid taskId, string userId, string? tenantId = null) => _engineService.ClaimTaskAsync(taskId, userId, tenantId);
    public Task CompleteTaskAsync(Guid taskId, IDictionary<string, object?>? variables = null, string? tenantId = null) => _engineService.CompleteTaskAsync(taskId, variables, tenantId);
    public Task DelegateTaskAsync(Guid taskId, string userId, string? tenantId = null) => _engineService.DelegateTaskAsync(taskId, userId, tenantId);
}
