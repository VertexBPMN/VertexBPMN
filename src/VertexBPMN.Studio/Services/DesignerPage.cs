namespace VertexBPMN.Studio.Services;

/// <summary>A designer page: identity, route and an ordered component list.</summary>
internal sealed class DesignerPage(string id, string route, string title, List<DesignerComponent> components)
{
    public string Id { get; } = id;
    public string Route { get; } = route;
    public string Title { get; } = title;
    public System.Text.Json.Nodes.JsonObject Metadata { get; init; } = new();
    public List<DesignerComponent> Components { get; } = components;
}
