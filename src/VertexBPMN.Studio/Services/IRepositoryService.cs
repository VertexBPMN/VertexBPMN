using Microsoft.AspNetCore.Components.Forms;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

public interface IRepositoryService
{
    Task DeployAsync(IBrowserFile file, string? tenantId = null);
    Task<ProcessDefinition> DeployXmlAsync(string xml, string name, string? tenantId = null);
    Task<IEnumerable<Deployment>> GetDeploymentsAsync(string? tenantId = null);
    Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionsAsync(string? tenantId = null);
    Task<string> GetProcessDefinitionXmlAsync(string processDefinitionId, string? tenantId = null);
    Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionVersionsAsync(string key, string? tenantId = null);
    Task DeleteProcessDefinitionAsync(Guid processDefinitionId, string? tenantId = null);
}

public sealed class RepositoryService : IRepositoryService
{
    private readonly IBpmnEngineService _engineService;

    public RepositoryService(IBpmnEngineService engineService) => _engineService = engineService;

    public Task DeployAsync(IBrowserFile file, string? tenantId = null) => _engineService.DeployAsync(file, tenantId);
    public Task<ProcessDefinition> DeployXmlAsync(string xml, string name, string? tenantId = null) => _engineService.DeployXmlAsync(xml, name, tenantId);
    public Task<IEnumerable<Deployment>> GetDeploymentsAsync(string? tenantId = null) => _engineService.GetDeploymentsAsync(tenantId);
    public Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionsAsync(string? tenantId = null) => _engineService.GetProcessDefinitionsAsync(tenantId);
    public Task<string> GetProcessDefinitionXmlAsync(string processDefinitionId, string? tenantId = null) => _engineService.GetProcessDefinitionXmlAsync(processDefinitionId, tenantId);
    public Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionVersionsAsync(string key, string? tenantId = null) => _engineService.GetProcessDefinitionVersionsAsync(key, tenantId);
    public Task DeleteProcessDefinitionAsync(Guid processDefinitionId, string? tenantId = null) => _engineService.DeleteProcessDefinitionAsync(processDefinitionId, tenantId);
}
