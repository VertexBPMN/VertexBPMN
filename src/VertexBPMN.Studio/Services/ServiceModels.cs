namespace VertexBPMN.Studio.Services;

public class Deployment
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime DeploymentTime { get; set; } = DateTime.Now;
    public string? TenantId { get; set; }
}

public class EngineConfiguration
{
    public string StatusMessage { get; set; } = string.Empty;
    public int DeploymentDelayMs { get; set; }
}

public class EngineConnection
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}