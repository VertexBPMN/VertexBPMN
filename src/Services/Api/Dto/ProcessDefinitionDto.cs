using System;

namespace VertexBPMN.Api.Dto;

public class ProcessDefinitionDto
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Resource { get; set; } = string.Empty;
    public string DeploymentId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public bool Suspended { get; set; }
}
