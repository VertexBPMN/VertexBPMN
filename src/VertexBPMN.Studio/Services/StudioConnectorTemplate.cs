using System.Text.Json.Serialization;

namespace VertexBPMN.Studio.Services;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract used by framework-discovered MVC controllers or Razor component parameters.")]
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
