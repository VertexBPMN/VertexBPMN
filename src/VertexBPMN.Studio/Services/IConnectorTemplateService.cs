using System.Text.Json.Serialization;

namespace VertexBPMN.Studio.Services;

public interface IConnectorTemplateService
{
    Task<IReadOnlyList<StudioConnectorTemplate>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
}

public sealed record StudioConnectorTemplateProperty(
    string Key,
    string Type,
    bool Required,
    [property: JsonPropertyName("default")] string? DefaultValue,
    IReadOnlyList<string>? Options);

public sealed record StudioConnectorTemplate(
    string Id,
    string TenantId,
    string Name,
    string Category,
    IReadOnlyList<string> AppliesTo,
    string Runtime,
    string? Icon,
    IReadOnlyList<StudioConnectorTemplateProperty> Properties,
    DateTime CreatedAt,
    DateTime LastModified);
