namespace VertexBPMN.Sdk;

public enum VertexBpmnEngineType
{
    Simple,
    Distributed
}

public sealed record EngineCapabilities(
    VertexBpmnEngineType EngineType,
    bool SupportsCmmn,
    bool SupportsWorkers,
    bool SupportsDurablePersistence);

public sealed record ProcessDefinition(
    string Id,
    string Key,
    string Name,
    int Version,
    string TenantId,
    bool Suspended);

public sealed record ProcessInstance(
    Guid Id,
    string BusinessKey,
    string ProcessDefinitionId,
    string ProcessDefinitionKey,
    string TenantId,
    string State,
    IDictionary<string, object?>? Variables);

public sealed record UserTask(
    Guid Id,
    string Name,
    string Assignee,
    DateTime Created,
    string? FormKey,
    string? FormSchema);

public sealed record FormSchema(Guid Id, string? FormKey, string? Schema);

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

public sealed record WorkflowTriggerCreated(
    WorkflowTrigger Trigger,
    string Secret,
    string InvokePath);
