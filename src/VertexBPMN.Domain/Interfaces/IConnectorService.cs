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

public sealed record ConnectorMetadata(string Id, string TenantId, string Name, string Type, string? Description, string? Endpoint, string? CredentialId, string? TemplateId, bool Enabled, DateTime CreatedAt, DateTime LastModified);
public sealed record ConnectorWriteRequest(string Name, string Type, string? Description, string? Endpoint, string? CredentialId, string? TemplateId, bool Enabled = true);
/// <summary>Redacted readiness result. It never contains credential material, request bodies or response bodies.</summary>
public sealed record ConnectorTestResult(bool Success, string Message, string? EndpointHost, bool CredentialConfigured);
