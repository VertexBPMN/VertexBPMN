using Microsoft.AspNetCore.Components.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services
{
    public interface IBpmnEngineService
    {
        Task DeployAsync(IBrowserFile file);
        Task<IEnumerable<Deployment>> GetDeploymentsAsync();
        Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionsAsync();
        Task<IEnumerable<ProcessInstance>> GetProcessInstancesAsync();
        Task<IEnumerable<UserTask>> GetTasksAsync();
        Task<string> GetProcessDefinitionXmlAsync(string processDefinitionId);
        Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionVersionsAsync(string key);
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