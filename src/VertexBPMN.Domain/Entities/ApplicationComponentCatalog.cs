using System.Collections.Frozen;
using System.Text.Json;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Domain.Entities;

public static class ApplicationComponentCatalog
{
    public static IReadOnlySet<string> Components { get; } = new[] { "table", "detail-card", "text", "form-js", "button", "status-diagram", "page-container" }.ToFrozenSet(StringComparer.Ordinal);
    public static IReadOnlySet<string> Actions { get; } = new[] { "data-source-read", "start-process", "claim-task", "complete-task", "navigate", "show-async-operation" }.ToFrozenSet(StringComparer.Ordinal);

    public static void Validate(string content)
    {
        using var document = JsonDocument.Parse(content);
        if (document.RootElement.TryGetProperty("localizations", out var localizations))
        {
            if (localizations.ValueKind != JsonValueKind.Object) { throw new ApplicationValidationException("Invalid localizations."); }
            foreach (var translationValue in localizations.EnumerateObject().Select(locale => locale.Value))
            {
                if (translationValue.ValueKind != JsonValueKind.Object || translationValue.EnumerateObject().Any(x => x.Value.ValueKind != JsonValueKind.String))
                { throw new ApplicationValidationException("Translations must be strings."); }
            }
        }
        if (!document.RootElement.TryGetProperty("pages", out var pages)) { return; }
        ApplicationReferenceGraph.Read(pages).Validate();
        var routes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages.EnumerateArray())
        {
            var route = page.GetProperty("route").GetString()!;
            if (string.IsNullOrWhiteSpace(route) || !route.StartsWith('/') || route.Contains("..", StringComparison.Ordinal)
                || route.Contains("//", StringComparison.Ordinal) || route.Any(char.IsWhiteSpace) || route.Contains('%', StringComparison.Ordinal)
                || route.Contains('?', StringComparison.Ordinal) || route.Contains('#', StringComparison.Ordinal) || route.Contains('\\', StringComparison.Ordinal))
            { throw new ApplicationValidationException("Invalid page route."); }
            if (!routes.Add(route)) { throw new ApplicationValidationException("Duplicate route."); }
            if (page.TryGetProperty("actions", out var actions))
            {
                foreach (var action in actions.EnumerateArray()) { RequireKnownAction(action); }
            }
            if (!page.TryGetProperty("components", out var components)) { continue; }
            foreach (var component in components.EnumerateArray())
            {
                RequireKnownComponent(component);
                if (component.TryGetProperty("action", out var action)) { RequireKnownAction(action); }
            }
        }
    }

    private static void RequireKnownAction(JsonElement action)
    {
        if (!Actions.Contains(action.GetProperty("type").GetString()!) || action.GetProperty("version").GetString() != "1.0.0")
        { throw new ApplicationValidationException("Unknown action type or version."); }
    }

    private static void RequireKnownComponent(JsonElement component)
    {
        if (!Components.Contains(component.GetProperty("type").GetString()!) || component.GetProperty("version").GetString() != "1.0.0")
        { throw new ApplicationValidationException("Unknown component type or version."); }
    }
}
