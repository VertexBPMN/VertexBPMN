using System.Text.Json.Serialization;

namespace VertexBPMN.Domain.Interfaces;

public interface IConnectorTemplateService
{
	Task<IReadOnlyList<ConnectorTemplateMetadata>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
	Task<ConnectorTemplateMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default);
	Task<ConnectorTemplateMetadata> CreateAsync(string tenantId, ConnectorTemplateWriteRequest request, CancellationToken cancellationToken = default);
	Task<bool> UpdateAsync(string tenantId, string id, ConnectorTemplateWriteRequest request, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
