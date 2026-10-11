using Microsoft.AspNetCore.Components.Forms;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Services;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "The public MVC controller exposes this dependency in its constructor.")]
public interface IRepositoryService
{
    Task DeployAsync(IBrowserFile file, string? tenantId = null);
    Task<StudioDeployResult> DeployXmlAsync(string xml, string name, string? tenantId = null);
    Task<IEnumerable<Deployment>> GetDeploymentsAsync(string? tenantId = null);
    Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionsAsync(string? tenantId = null);
    Task<string> GetProcessDefinitionXmlAsync(string processDefinitionId, string? tenantId = null);
    Task<IEnumerable<ProcessDefinition>> GetProcessDefinitionVersionsAsync(string key, string? tenantId = null);
    Task DeleteProcessDefinitionAsync(Guid processDefinitionId, string? tenantId = null);
}
