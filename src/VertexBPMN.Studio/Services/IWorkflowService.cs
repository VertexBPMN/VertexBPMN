using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

internal interface IWorkflowService
{
    Task<IEnumerable<ProcessInstance>> GetProcessInstancesAsync(string? tenantId = null);
    Task<ProcessInstance> StartProcessAsync(string processDefinitionKey, IDictionary<string, object?>? variables = null, string? businessKey = null, string? tenantId = null);
    Task SuspendProcessInstanceAsync(Guid instanceId, string? tenantId = null);
    Task ResumeProcessInstanceAsync(Guid instanceId, string? tenantId = null);
    Task DeleteProcessInstanceAsync(Guid instanceId, string? tenantId = null);
}
