using Microsoft.AspNetCore.Components.Forms;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

internal sealed class RepositoryService : IRepositoryService
{
    private readonly IBpmnEngineService _engineService;

    public RepositoryService(IBpmnEngineService engineService) => _engineService = engineService;

    public Task DeployAsync(IBrowserFile file, string? tenantId = null) => _engineService.DeployAsync(file, tenantId);
    public Task<StudioDeployResult> DeployXmlAsync(string xml, string name, string? tenantId = null) => _engineService.DeployXmlAsync(xml, name, tenantId);
    public Task<IEnumerable<Deployment>> GetDeploymentsAsync(string? tenantId = null) => _engineService.GetDeploymentsAsync(tenantId);
    public Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionsAsync(string? tenantId = null) => _engineService.GetProcessDefinitionsAsync(tenantId);
    public Task<string> GetProcessDefinitionXmlAsync(string processDefinitionId, string? tenantId = null) => _engineService.GetProcessDefinitionXmlAsync(processDefinitionId, tenantId);
    public Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionVersionsAsync(string key, string? tenantId = null) => _engineService.GetProcessDefinitionVersionsAsync(key, tenantId);
    public Task DeleteProcessDefinitionAsync(Guid processDefinitionId, string? tenantId = null) => _engineService.DeleteProcessDefinitionAsync(processDefinitionId, tenantId);
}
