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

public sealed class StudioWorkflowTrigger
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime? LastTriggeredAt { get; set; }
    public long InvocationCount { get; set; }
    public string? Path { get; set; }
    public string? Method { get; set; }
    public string AuthenticationMode { get; set; } = string.Empty;
    public string? CredentialId { get; set; }
    public string? CorrelationKey { get; set; }
}

public sealed class StudioWorkflowTriggerCreated
{
    public StudioWorkflowTrigger Trigger { get; set; } = new();
    public string Secret { get; set; } = string.Empty;
    public string InvokePath { get; set; } = string.Empty;
}

public sealed class StudioProcessInstance
{
    public Guid Id { get; set; }
    public string ProcessId { get; set; } = string.Empty;
    public string? BusinessKey { get; set; }
    public string? TenantId { get; set; }
    public string State { get; set; } = string.Empty;
}
