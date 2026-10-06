using System.Text.Json.Serialization;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ConnectorTemplateProperty(string Key, string Type, bool Required = false, [property: JsonPropertyName("default")] string? DefaultValue = null, IReadOnlyList<string>? Options = null);
