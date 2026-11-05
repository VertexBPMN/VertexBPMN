using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

internal sealed class TaskService : ITaskService
{
    private readonly IBpmnEngineService _engineService;

    public TaskService(IBpmnEngineService engineService) => _engineService = engineService;

    public Task<IEnumerable<StudioUserTask>> GetTasksAsync(string? tenantId = null) => _engineService.GetTasksAsync(tenantId);
    public Task ClaimTaskAsync(Guid taskId, string userId, string? tenantId = null) => _engineService.ClaimTaskAsync(taskId, userId, tenantId);
    public Task CompleteTaskAsync(Guid taskId, IDictionary<string, object?>? variables = null, string? tenantId = null, long? expectedRevision = null, string? formSchemaHash = null, string? idempotencyKey = null) => _engineService.CompleteTaskAsync(taskId, variables, tenantId, expectedRevision, formSchemaHash, idempotencyKey);
    public Task DelegateTaskAsync(Guid taskId, string userId, string? tenantId = null) => _engineService.DelegateTaskAsync(taskId, userId, tenantId);
}
