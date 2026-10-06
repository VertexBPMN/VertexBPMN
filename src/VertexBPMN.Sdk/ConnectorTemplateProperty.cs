namespace VertexBPMN.Sdk;
public sealed record ConnectorTemplateProperty(string Key, string Type, bool Required = false, string? DefaultValue = null, IReadOnlyList<string>? Options = null);
