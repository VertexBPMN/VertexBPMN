namespace VertexBPMN.Studio.Services;

public interface IConnectorService
{
    Task<IReadOnlyList<StudioConnector>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<StudioConnector> CreateAsync(string tenantId, string name, string type, string? description, string? endpoint, string? credentialId, string? templateId, bool enabled = true, CancellationToken cancellationToken = default);
    Task UpdateAsync(string tenantId, string id, string name, string type, string? description, string? endpoint, string? credentialId, string? templateId, bool enabled, CancellationToken cancellationToken = default);
    Task SetEnabledAsync(string tenantId, string id, bool enabled, CancellationToken cancellationToken = default);
    Task DeleteAsync(string tenantId, string id, CancellationToken cancellationToken = default);
    Task<StudioConnectorTestResult> TestAsync(string tenantId, string id, CancellationToken cancellationToken = default);
}
public sealed record StudioConnector(string Id, string TenantId, string Name, string Type, string? Description, string? Endpoint, string? CredentialId, string? TemplateId, bool Enabled, DateTime CreatedAt, DateTime LastModified);
public sealed record StudioConnectorTestResult(bool Success, string Message, string? EndpointHost, bool CredentialConfigured);
