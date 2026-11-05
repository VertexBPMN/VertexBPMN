using VertexBPMN.Application.Lc06;
using VertexBPMN.Portal.Client.Rendering;

namespace VertexBPMN.Tests.Unit.Portal;

/// <summary>
/// LC06 reference application (LC-AC-08): the released ContentJson must be accepted by the
/// allowlist renderer — every component resolves to a KnownComponent and there are zero
/// UnsupportedComponent placeholders.
/// </summary>
public sealed class ReferenceAppRendererTests
{
    private static readonly HashSet<string> AllowedComponentTypes = new(StringComparer.Ordinal)
        { "table", "detail-card", "text", "form-js", "button", "status-diagram", "page-container" };

    private static readonly string[] ReferenceRoutes = { "/home", "/tasks", "/settings" };

    [Fact]
    public void Reference_app_content_renders_with_zero_unsupported_components()
    {
        var app = ReleasedRenderer.Render(Lc06ReferenceApp.ContentJson);

        Assert.Equal(0, app.UnsupportedCount);
        Assert.Equal(3, app.Pages.Count);
        Assert.Equal(ReferenceRoutes, app.Pages.Select(p => p.Route).ToArray(), StringComparer.Ordinal);
        // Every rendered node, including nested container children, is a KnownComponent.
        Assert.All(Flatten(app.Pages), node => Assert.IsType<KnownComponent>(node));
    }

    [Fact]
    public void Reference_app_exercises_every_allowlisted_component_type()
    {
        var app = ReleasedRenderer.Render(Lc06ReferenceApp.ContentJson);
        var types = Flatten(app.Pages).OfType<KnownComponent>().Select(n => n.Type).ToHashSet(StringComparer.Ordinal);

        // The reference app must cover the whole allowlist (no dead component types)…
        Assert.Superset(types, AllowedComponentTypes);
        // …and never introduce a type outside the allowlist.
        Assert.Subset(types, AllowedComponentTypes);
    }

    private static IEnumerable<RenderedNode> Flatten(IReadOnlyList<RenderedPage> pages)
        => pages.SelectMany(p => p.Components).SelectMany(Flatten);

    private static IEnumerable<RenderedNode> Flatten(RenderedNode node)
        => node is KnownComponent known
            ? new[] { (RenderedNode)known }.Concat(known.Children.SelectMany(Flatten))
            : new[] { node };
}
