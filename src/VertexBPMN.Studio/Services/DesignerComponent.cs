namespace VertexBPMN.Studio.Services;

/// <summary>
/// A designer component: allowlist type + version, editable properties, children and
/// (LC07 S2) the ordered list of data-source field mappings authored for it. A mapping
/// reuses the Studio FieldMapper convention (source path = data-source field, target key =
/// table column / component field) and is validated against the bound data source's schema.
/// </summary>
internal sealed class DesignerComponent(string id, string type, string version, string? label, string? dataSource, List<DesignerComponent> children, List<StudioFieldMappingItem>? mappings = null)
{
    public string Id { get; } = id;
    public string Type { get; } = type;
    public string Version { get; } = version;
    public string? Label { get; set; } = label;
    public string? DataSource { get; set; } = dataSource;
    public List<DesignerComponent> Children { get; } = children;
    public List<StudioFieldMappingItem> Mappings { get; } = mappings ?? [];
    /// <summary>The explicit action authored on this component (LC07 S3), or <c>null</c> when it has none.</summary>
    public DesignerAction? Action { get; set; }
}
