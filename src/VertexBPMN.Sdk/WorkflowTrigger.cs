namespace VertexBPMN.Sdk;

public sealed record WorkflowTrigger(
    Guid Id,
    string Name,
    string ProcessDefinitionKey,
    string? TenantId,
    bool Enabled,
    DateTime CreatedAt,
    DateTime LastModified,
    DateTime? LastTriggeredAt,
    long InvocationCount);
