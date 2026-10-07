namespace VertexBPMN.Sdk;

public sealed record ProcessInstance(
    Guid Id,
    string BusinessKey,
    string ProcessDefinitionId,
    string ProcessDefinitionKey,
    string TenantId,
    string State,
    IDictionary<string, object?>? Variables);
