using System;

namespace VertexBPMN.Api.Dto;

public class DeploymentDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime DeploymentTime { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
