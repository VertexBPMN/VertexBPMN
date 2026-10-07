namespace VertexBPMN.Sdk;
public sealed record ConnectorTemplateMetadata(string Id, string TenantId, string Name, string Category, IReadOnlyList<string> AppliesTo, string Runtime, string? Icon, IReadOnlyList<ConnectorTemplateProperty> Properties, DateTime CreatedAt, DateTime LastModified);
