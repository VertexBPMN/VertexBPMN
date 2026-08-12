using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

public interface IWorkflowService
{
    Task<IEnumerable<ProcessInstance>> GetProcessInstancesAsync(string? tenantId = null);
    Task<ProcessInstance> StartProcessAsync(string processDefinitionKey, IDictionary<string, object?>? variables = null, string? businessKey = null, string? tenantId = null);
    Task SuspendProcessInstanceAsync(Guid instanceId, string? tenantId = null);
    Task ResumeProcessInstanceAsync(Guid instanceId, string? tenantId = null);
    Task DeleteProcessInstanceAsync(Guid instanceId, string? tenantId = null);
}

public sealed class WorkflowService : IWorkflowService
{
    private readonly IBpmnEngineService _engineService;

    public WorkflowService(IBpmnEngineService engineService)
    {
        _engineService = engineService;
    }

    public Task<IEnumerable<ProcessInstance>> GetProcessInstancesAsync(string? tenantId = null) =>
        _engineService.GetProcessInstancesAsync(tenantId);

    public Task<ProcessInstance> StartProcessAsync(string processDefinitionKey, IDictionary<string, object?>? variables = null, string? businessKey = null, string? tenantId = null) =>
        _engineService.StartProcessAsync(processDefinitionKey, variables, businessKey, tenantId);

    public Task SuspendProcessInstanceAsync(Guid instanceId, string? tenantId = null) =>
        _engineService.SuspendProcessInstanceAsync(instanceId, tenantId);

    public Task ResumeProcessInstanceAsync(Guid instanceId, string? tenantId = null) =>
        _engineService.ResumeProcessInstanceAsync(instanceId, tenantId);

    public Task DeleteProcessInstanceAsync(Guid instanceId, string? tenantId = null) =>
        _engineService.DeleteProcessInstanceAsync(instanceId, tenantId);
}
