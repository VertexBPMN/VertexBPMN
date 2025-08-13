using System;

namespace VertexBPMN.Persistence.Models;

public class ProcessDefinition
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BpmnXml { get; set; } = string.Empty;
    public Guid DeploymentId { get; set; }
    public EngineDeployment Deployment { get; set; } = null!;
    public string? TenantId { get; set; }
}
