namespace VertexBPMN.Api.Dto;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract type DeploymentDto retained for exposed HTTP action signatures and controller activation (DeploymentDto).")]
public class DeploymentDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime DeploymentTime { get; set; }
    public string TenantId { get; set; } = string.Empty;
}
