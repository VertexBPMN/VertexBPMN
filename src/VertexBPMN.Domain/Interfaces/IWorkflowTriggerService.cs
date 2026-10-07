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
	Task<IReadOnlyList<WorkflowTriggerCreated>> SynchronizeBpmnWebhooksAsync(string bpmnXml, string processDefinitionKey, string? tenantId = null, CancellationToken cancellationToken = default);
	Task<WorkflowTriggerInvocationResult> InvokeWebhookAsync(string path, string method, string? triggerSecret, string? signature, ReadOnlyMemory<byte> payload, string? timestamp = null, string? deliveryId = null, CancellationToken cancellationToken = default);
}
