using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public interface IWorkflowTriggerService
{
    Task<IReadOnlyList<WorkflowTriggerInfo>> ListAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<WorkflowTriggerInfo?> GetAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<WorkflowTriggerCreated> CreateAsync(string name, string processDefinitionKey, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid id, string? name, bool? enabled, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<WorkflowTriggerInvocationResult> InvokeAsync(Guid id, string secret, IDictionary<string, object?>? variables = null, string? businessKey = null, CancellationToken cancellationToken = default);
}

public sealed record WorkflowTriggerInfo(
    Guid Id,
    string Name,
    string ProcessDefinitionKey,
    string? TenantId,
    bool Enabled,
    DateTime CreatedAt,
    DateTime LastModified,
    DateTime? LastTriggeredAt,
    long InvocationCount);

public sealed record WorkflowTriggerCreated(WorkflowTriggerInfo Trigger, string Secret, string InvokePath);

public sealed record WorkflowTriggerInvocationResult(
    WorkflowTriggerInvocationStatus Status,
    ProcessInstance? ProcessInstance = null);

public enum WorkflowTriggerInvocationStatus
{
    Started,
    NotFound,
    InvalidSecret,
    Disabled,
    ProcessDefinitionNotFound
}
