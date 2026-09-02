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

public sealed record ConnectorTemplateProperty(string Key, string Type, bool Required = false, [property: JsonPropertyName("default")] string? DefaultValue = null, IReadOnlyList<string>? Options = null);

public sealed record ConnectorTemplateMetadata(
    string Id,
    string TenantId,
    string Name,
    string Category,
    IReadOnlyList<string> AppliesTo,
    string Runtime,
    string? Icon,
    IReadOnlyList<ConnectorTemplateProperty> Properties,
    DateTime CreatedAt,
    DateTime LastModified);

public sealed record ConnectorTemplateWriteRequest(
    string Name,
    string Category,
    IReadOnlyList<string> AppliesTo,
    string Runtime,
    string? Icon,
    IReadOnlyList<ConnectorTemplateProperty> Properties);

public sealed class ConnectorTemplateConflictException(string message) : Exception(message);
