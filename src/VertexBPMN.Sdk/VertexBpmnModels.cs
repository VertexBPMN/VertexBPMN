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

public sealed record CredentialMetadata(string Id, string TenantId, string Name, string Type, string? Description, IReadOnlyList<string> SecretKeys, DateTime CreatedAt, DateTime LastModified, DateTime? LastUsedAt);
public sealed record ConnectorWriteRequest(string Name, string Type, string? Description, string? Endpoint, string? CredentialId, string? TemplateId = null, bool Enabled = true);
public sealed record ConnectorMetadata(string Id, string TenantId, string Name, string Type, string? Description, string? Endpoint, string? CredentialId, string? TemplateId, bool Enabled, DateTime CreatedAt, DateTime LastModified);
public sealed record ConnectorTestResult(bool Success, string Message, string? EndpointHost, bool CredentialConfigured);
public sealed record ConnectorTemplateProperty(string Key, string Type, bool Required = false, string? DefaultValue = null, IReadOnlyList<string>? Options = null);
public sealed record ConnectorTemplateMetadata(string Id, string TenantId, string Name, string Category, IReadOnlyList<string> AppliesTo, string Runtime, string? Icon, IReadOnlyList<ConnectorTemplateProperty> Properties, DateTime CreatedAt, DateTime LastModified);
public sealed record FormDefinitionMetadata(string Id, string TenantId, string Key, string Name, string Schema, int Version, DateTime CreatedAt, DateTime LastModified);
public sealed record SemanticValidationResult(bool IsValid, IReadOnlyList<string>? Errors, IReadOnlyList<string>? Warnings, IReadOnlyList<string>? Suggestions);
public sealed record DecisionResult(IDictionary<string, object?> Variables);
public sealed record TestRunResult(ProcessDefinition Definition, ProcessInstance Instance);
public sealed record RuntimeExecutionTrace(Guid SessionId, Guid ProcessInstanceId, DateTime StartedAt, DateTime? EndedAt, IReadOnlyList<RuntimeTraceEvent> Events, RuntimePerformanceMetrics? PerformanceMetrics);
public sealed record RuntimeTraceEvent(string Type, string ActivityId, DateTime Timestamp, string? Details, TimeSpan? Duration);
public sealed record RuntimePerformanceMetrics(int TotalEvents, TimeSpan TotalExecutionTime, DateTime? FastestEventTime, DateTime? SlowestEventTime);
public sealed record CaseDefinition(string Id, string TenantId, string Key, string Name, string CmmnXml, DateTime CreatedAt, DateTime LastModified);
public sealed record CaseRunResult(string CaseDefinitionId, string Key, IReadOnlyList<string> Trace);
public sealed record N8nImportResult(string BpmnXml, IReadOnlyList<N8nImportReportItem> Report);
public sealed record N8nImportReportItem(string NodeName, string NodeType, string Disposition, string Message);
