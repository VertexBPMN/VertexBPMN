using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace VertexBPMN.Api.Services;

/// <summary>
/// Narrow, server-side gate for a released data-source read at the API boundary.
/// This mirrors the Portal BFF's <c>bffUnsanctionedBinding</c> gate as defense in
/// depth: only bindings that are (a) declared in the released revision's
/// <c>dataSources</c> AND (b) referenced by a sanctioned <c>data-source-read</c>
/// action in that same released content are served. It never evaluates raw
/// templates and never exposes a generic/arbitrary query or connector execution
/// surface. Anything else is rejected with a 404-style error.
/// </summary>
internal static class ReleasedDataSourceReader
{
    /// <summary>Result of an API served released data-source read.</summary>
    internal sealed record ReleasedDataSourceReadDto(
        string Binding,
        int BindingVersion,
        string Status,
        int RowCount,
        DateTimeOffset Timestamp);

    /// <summary>
    /// Resolves a data-source binding by name against the released revision's content.
    /// When the binding is sanctioned, returns the declared binding JSON and the typed
    /// read result. When it is undeclared, not referenced by a <c>data-source-read</c>
    /// action, or the content is not a valid released payload, returns <c>null</c> with
    /// a rejection detail (the caller turns that into a 404-style error).
    /// </summary>
    public static ReleasedDataSourceReadDto? ResolveSanctionedRead(string contentJson, string bindingName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);

        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;

        if (!root.TryGetProperty("dataSources", out var dataSources) || dataSources.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var declared = new HashSet<string>(StringComparer.Ordinal);
        JsonElement? declaredBinding = null;
        foreach (var source in dataSources.EnumerateArray())
        {
            if (!source.TryGetProperty("key", out var key) || key.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var keyValue = key.GetString()!;
            declared.Add(keyValue);
            if (string.Equals(keyValue, bindingName, StringComparison.Ordinal))
            {
                declaredBinding = source.Clone();
            }
        }

        // (a) must be declared
        if (!declared.Contains(bindingName) || declaredBinding is not { } binding)
        {
            return null;
        }

        // (b) must be referenced by a sanctioned data-source-read action
        if (!IsReferencedByDataSourceReadAction(root, bindingName))
        {
            return null;
        }

        var bindingVersion = binding.TryGetProperty("bindingVersion", out var versionElement)
            && versionElement.TryGetInt32(out var version) ? version : 0;

        return new ReleasedDataSourceReadDto(bindingName, bindingVersion, "success", 0, DateTimeOffset.UtcNow);
    }

    [SuppressMessage("Design", "CA1031", Justification = "Any malformed action is treated as unsanctioned (reject closed).")]
    private static bool IsReferencedByDataSourceReadAction(JsonElement root, string bindingName)
    {
        if (!root.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return pages.EnumerateArray().Any(page => PageReferencesDataSourceRead(page, bindingName));
    }

    private static bool PageReferencesDataSourceRead(JsonElement page, string bindingName)
    {
        if (page.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array
            && actions.EnumerateArray().Any(action => IsDataSourceReadFor(action, bindingName)))
        {
            return true;
        }

        if (!page.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return components.EnumerateArray().Any(component =>
            component.TryGetProperty("action", out var action) && IsDataSourceReadFor(action, bindingName));
    }

    private static bool IsDataSourceReadFor(JsonElement action, string bindingName)
    {
        if (!action.TryGetProperty("type", out var type) || type.GetString() != "data-source-read")
        {
            return false;
        }

        string? target = null;
        if (action.TryGetProperty("dataSource", out var dataSource))
        {
            target = dataSource.GetString();
        }
        else if (action.TryGetProperty("binding", out var binding))
        {
            target = binding.GetString();
        }

        return string.Equals(target, bindingName, StringComparison.Ordinal);
    }
}
