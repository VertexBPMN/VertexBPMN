namespace VertexBPMN.Domain.Entities;

/// <summary>
/// Persisted inbound trigger that starts a registered BPMN process.
/// </summary>
public sealed class WorkflowTrigger
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ProcessDefinitionKey { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string SecretHash { get; set; } = string.Empty;
    public string? Path { get; set; }
    public string? Method { get; set; }
    public string AuthenticationMode { get; set; } = "trigger-secret";
    public string? CredentialId { get; set; }
    public string? CredentialSecretKey { get; set; }
    public string? PayloadSchemaJson { get; set; }
    public string? CorrelationKey { get; set; }
    public string? SourceElementId { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAt { get; set; }
    public long InvocationCount { get; set; }
}
