namespace VertexBPMN.Domain.Interfaces;

public interface IFormDefinitionService
{
	Task<IReadOnlyList<FormDefinitionMetadata>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
	Task<FormDefinitionMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default);
	Task<FormDefinitionMetadata> CreateAsync(string tenantId, FormDefinitionWriteRequest request, CancellationToken cancellationToken = default);
	Task<bool> UpdateAsync(string tenantId, string id, FormDefinitionWriteRequest request, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
