namespace VertexBPMN.Studio.Services;

internal sealed class StudioWorkflowTrigger
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
