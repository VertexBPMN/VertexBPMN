using Microsoft.AspNetCore.Components.Forms;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services
{
    public interface IBpmnEngineService
    {
        Task DeployAsync(IBrowserFile file, string? tenantId = null);
        Task<ProcessDefinition> DeployXmlAsync(string xml, string name, string? tenantId = null);
        Task<IEnumerable<Deployment>> GetDeploymentsAsync(string? tenantId = null);
        Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionsAsync(string? tenantId = null);
        Task<IEnumerable<ProcessInstance>> GetProcessInstancesAsync(string? tenantId = null);
        Task<IEnumerable<UserTask>> GetTasksAsync(string? tenantId = null);
        Task<string> GetProcessDefinitionXmlAsync(string processDefinitionId, string? tenantId = null);
        Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionVersionsAsync(string key, string? tenantId = null);
        Task DeleteProcessDefinitionAsync(Guid processDefinitionId, string? tenantId = null);
        Task<ProcessInstance> StartProcessAsync(string processDefinitionKey, IDictionary<string, object?>? variables = null, string? businessKey = null, string? tenantId = null);
        Task ClaimTaskAsync(Guid taskId, string userId, string? tenantId = null);
        Task CompleteTaskAsync(Guid taskId, IDictionary<string, object?>? variables = null, string? tenantId = null);
        Task DelegateTaskAsync(Guid taskId, string userId, string? tenantId = null);
        Task SuspendProcessInstanceAsync(Guid instanceId, string? tenantId = null);
        Task ResumeProcessInstanceAsync(Guid instanceId, string? tenantId = null);
        Task DeleteProcessInstanceAsync(Guid instanceId, string? tenantId = null);
        Task RollbackProcessDefinitionAsync(string engineId, string processDefinitionId);
        Task<EngineConfiguration> GetEngineConfigurationAsync();
        Task UpdateEngineConfigurationAsync(EngineConfiguration configuration);
        Task<IEnumerable<EngineConnection>> GetEngineConnectionsAsync();
        Task AddEngineConnectionAsync(EngineConnection connection);
        Task UpdateEngineConnectionAsync(EngineConnection connection);
        Task RemoveEngineConnectionAsync(string connectionId);
        event Action<string> OnEventEmitted;
    }

}