using VertexBPMN.Portal.Client.Rendering;

namespace VertexBPMN.Tests.Unit.Portal;

/// <summary>
/// LC06 client renderer slice: the recursive allowlist renderer must render known/unsanctioned
/// components and fall back to a safe placeholder for anything unknown — never evaluating,
/// reflecting or instantiating arbitrary component types from released metadata.
/// </summary>
public sealed class ReleasedRendererTests
{
    [Fact]
    public void Known_component_renders_as_known_node()
    {
        const string content = """{"pages":[{"id":"home","route":"/home","components":[{"type":"text","version":"1.0.0"}]}]}""";

        var app = ReleasedRenderer.Render(content);

        var page = Assert.Single(app.Pages);
        Assert.Equal("home", page.Id, StringComparer.Ordinal);
        var node = Assert.IsType<KnownComponent>(Assert.Single(page.Components));
        Assert.Equal("text", node.Type, StringComparer.Ordinal);
        Assert.Empty(node.Children);
        Assert.Equal(0, app.UnsupportedCount);
    }

    [Fact]
    public void Unknown_component_renders_as_safe_placeholder_without_evaluation()
    {
        // An unknown type carrying an executable-looking payload must be treated as inert.
        const string content = """{"pages":[{"id":"home","route":"/home","components":[{"type":"custom-execute","version":"1.0.0","expression":"alert('x')","html":"<script>"}]}]}""";

        var app = ReleasedRenderer.Render(content);

        var page = Assert.Single(app.Pages);
        var node = Assert.IsType<UnsupportedComponent>(Assert.Single(page.Components));
        Assert.Equal("custom-execute", node.Type, StringComparer.Ordinal);
        Assert.Equal(1, app.UnsupportedCount);
    }

    [Fact]
    public void Unsupported_version_renders_as_placeholder()
    {
        const string content = """{"pages":[{"id":"home","route":"/home","components":[{"type":"button","version":"9.9.9"}]}]}""";

        var app = ReleasedRenderer.Render(content);

        var node = Assert.IsType<UnsupportedComponent>(Assert.Single(Assert.Single(app.Pages).Components));
        Assert.Equal("button", node.Type, StringComparer.Ordinal);
        Assert.Equal(1, app.UnsupportedCount);
    }

    [Fact]
    public void Unknown_type_children_are_not_descended_into()
    {
        const string content = """{"pages":[{"id":"home","route":"/home","components":[{"type":"evil-container","version":"1.0.0","children":[{"type":"text","version":"1.0.0"}]}]}]}""";

        var app = ReleasedRenderer.Render(content);

        Assert.IsType<UnsupportedComponent>(Assert.Single(Assert.Single(app.Pages).Components));
        // The nested known component must never surface from inside an unsanctioned container.
        Assert.Equal(1, app.UnsupportedCount);
        Assert.Equal(0, app.Pages[0].Components.Count(n => n is KnownComponent));
    }

    [Fact]
    public void Container_children_are_rendered_recursively()
    {
        const string content = """{"pages":[{"id":"home","route":"/home","components":[{"type":"page-container","version":"1.0.0","children":[{"type":"table","version":"1.0.0"},{"type":"button","version":"1.0.0"}]}]}]}""";

        var app = ReleasedRenderer.Render(content);

        var container = Assert.IsType<KnownComponent>(Assert.Single(Assert.Single(app.Pages).Components));
        Assert.Equal("page-container", container.Type, StringComparer.Ordinal);
        Assert.Equal(2, container.Children.Count);
        Assert.All(container.Children, c => Assert.IsType<KnownComponent>(c));
    }

    [Fact]
    public void Allowlist_only_sinks_declared_component_types()
    {
        const string content = """{"pages":[{"id":"p","route":"/p","components":[{"type":"table","version":"1.0.0"},{"type":"label","version":"1.0.0"}]}]}""";

        var app = ReleasedRenderer.Render(content);

        var nodes = app.Pages[0].Components;
        Assert.IsType<KnownComponent>(nodes[0]);
        Assert.IsType<UnsupportedComponent>(nodes[1]);
    }
}
