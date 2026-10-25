using System;
using System.Collections.Generic;
using System.Text.Json;

namespace VertexBPMN.Portal.Client.Rendering;

/// <summary>
/// Pure, closed allowlist renderer for a released application's ContentJson. It walks the
/// component tree with a recursive allowlist of KNOWN component types and versions. Unknown
/// or unsanctioned component types become a safe <see cref="UnsupportedComponent"/> placeholder;
/// they are never evaluated, never reflected and never instantiated. No templates, no
/// expressions, no arbitrary HTML/component creation.
/// </summary>
public static class ReleasedRenderer
{
    /// <summary>The closed set of component types the renderer knows how to sink (mirrors the LC05 catalog).</summary>
    public static IReadOnlySet<string> Allowlist { get; } = new HashSet<string>(
        new[] { "table", "detail-card", "text", "form-js", "button", "status-diagram", "page-container" },
        StringComparer.Ordinal);

    /// <summary>Renders the released application's component tree from its ContentJson.</summary>
    public static RenderedApplication Render(string contentJson, IReadOnlySet<string>? allowlist = null)
    {
        var allowed = allowlist ?? Allowlist;
        var pages = new List<RenderedPage>();
        var unsupported = 0;

        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("pages", out var pagesElement) &&
            pagesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var page in pagesElement.EnumerateArray())
            {
                var components = new List<RenderedNode>();
                var pageUnsupported = 0;
                if (page.TryGetProperty("components", out var componentList) && componentList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var component in componentList.EnumerateArray())
                    {
                        RenderComponent(component, allowed, components, ref pageUnsupported);
                    }
                }

                pages.Add(new RenderedPage(
                    GetString(page, "id") ?? string.Empty,
                    GetString(page, "route") ?? string.Empty,
                    components));
                unsupported += pageUnsupported;
            }
        }

        return new RenderedApplication(pages, unsupported);
    }

    private static void RenderComponent(JsonElement component, IReadOnlySet<string> allowlist, List<RenderedNode> output, ref int unsupported)
    {
        var type = GetString(component, "type");
        var version = GetString(component, "version");
        if (type is null || !allowlist.Contains(type) || !string.Equals(version, "1.0.0", StringComparison.Ordinal))
        {
            // Unknown/unsanctioned type: safe placeholder; its payload is never descended into.
            output.Add(new UnsupportedComponent(type ?? "unknown"));
            unsupported++;
            return;
        }

        var children = new List<RenderedNode>();
        if (component.TryGetProperty("children", out var childrenElement) && childrenElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childrenElement.EnumerateArray())
            {
                RenderComponent(child, allowlist, children, ref unsupported);
            }
        }

        output.Add(new KnownComponent(type, children));
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
