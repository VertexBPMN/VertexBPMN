using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces.Repositories;

public interface IWorkflowTriggerRepository
{
    Task<IReadOnlyList<WorkflowTrigger>> ListAsync(string? tenantId = null, CancellationToken cancellationToken = default);
    Task<WorkflowTrigger?> GetAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<WorkflowTrigger?> GetByEndpointAsync(string path, string method, string? tenantId = null, CancellationToken cancellationToken = default);
    Task<WorkflowTrigger?> GetBySourceElementAsync(string processDefinitionKey, string sourceElementId, string? tenantId = null, CancellationToken cancellationToken = default);
    Task AddAsync(WorkflowTrigger trigger, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, string? tenantId = null, CancellationToken cancellationToken = default);
    Task SaveAsync(WorkflowTrigger trigger, CancellationToken cancellationToken = default);
}
