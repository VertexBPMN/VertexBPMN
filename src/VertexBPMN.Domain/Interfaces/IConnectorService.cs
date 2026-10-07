namespace VertexBPMN.Domain.Interfaces;

public interface IConnectorService
{
	Task<IReadOnlyList<ConnectorMetadata>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
	Task<ConnectorMetadata?> GetAsync(string tenantId, string id, CancellationToken cancellationToken = default);
	Task<ConnectorMetadata> CreateAsync(string tenantId, ConnectorWriteRequest request, CancellationToken cancellationToken = default);
	Task<bool> UpdateAsync(string tenantId, string id, ConnectorWriteRequest request, CancellationToken cancellationToken = default);
	Task<bool> SetEnabledAsync(string tenantId, string id, bool enabled, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
	Task<ConnectorTestResult?> TestAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
