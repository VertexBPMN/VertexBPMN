using System.Text.Json.Serialization;

namespace VertexBPMN.Studio.Services;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract used by framework-discovered MVC controllers or Razor component parameters.")]
public sealed record StudioConnectorTemplateProperty(
    string Key,
    string Type,
    bool Required,
    [property: JsonPropertyName("default")] string? DefaultValue,
    IReadOnlyList<string>? Options);
