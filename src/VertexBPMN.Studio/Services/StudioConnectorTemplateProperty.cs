using System.Text.Json.Serialization;

namespace VertexBPMN.Studio.Services;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public contract used by framework-discovered MVC controllers or Razor component parameters.")]
public sealed record StudioConnectorTemplateProperty(
    string Key,
    string Type,
    bool Required,
    [property: JsonPropertyName("default")] string? DefaultValue,
    IReadOnlyList<string>? Options,
    // LC02: the binding contract version the property participates in (1 = legacy flat,
    // 2 = versioned). Studio surfaces it so a template adopts the same contract as the
    // importer/runtime instead of being silently reinterpreted. Additive and optional.
    int BindingVersion = VertexBPMN.Domain.Interfaces.ConnectorBindingVersion.Legacy);
