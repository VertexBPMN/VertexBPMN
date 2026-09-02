namespace VertexBPMN.Studio.Services;

public interface IWorkflowTriggerService
{
    Task<IReadOnlyList<StudioWorkflowTrigger>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<StudioWorkflowTriggerCreated> CreateAsync(string tenantId, string name, string processDefinitionKey, CancellationToken cancellationToken = default);
    Task UpdateAsync(string tenantId, Guid id, string? name, bool? enabled, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<StudioProcessInstance?> InvokeAsync(Guid id, string secret, IDictionary<string, object?>? variables = null, string? businessKey = null, CancellationToken cancellationToken = default);
}
