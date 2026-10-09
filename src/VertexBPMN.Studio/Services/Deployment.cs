namespace VertexBPMN.Studio.Services;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract used by framework-discovered MVC controllers or Razor component parameters.")]
public class Deployment
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime DeploymentTime { get; set; } = DateTime.Now;
    public string? TenantId { get; set; }
}
