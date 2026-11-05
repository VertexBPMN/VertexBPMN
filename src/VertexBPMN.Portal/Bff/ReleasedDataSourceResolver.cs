using System.Text.Json;

namespace VertexBPMN.Portal.Bff;

/// <summary>
/// Narrow, server-side gate for data-source execution against the released
/// release. It never evaluates raw templates or exposes a generic execution
/// endpoint: only query bindings that are (a) declared in the released revision's
/// <c>dataSources</c> and (b) referenced by a sanctioned <c>data-source-read</c>
/// action in that same released content are allowed. Anything else is rejected.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public resolver invoked by the public BFF controller.")]
public static class ReleasedDataSourceResolver
{
    /// <summary>
    /// Resolves a data-source binding by name against the released revision. Throws
    /// <see cref="BffUnsanctionedBindingException"/> when the binding is undeclared,
    /// not referenced by a <c>data-source-read</c> action, or the content is not a
    /// valid released application payload. Returns the raw binding JSON element.
    /// </summary>
    public static JsonElement ResolveSanctionedBinding(string contentJson, string bindingName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);

        using var document = JsonDocument.Parse(contentJson);
        var root = document.RootElement;

        if (!root.TryGetProperty("dataSources", out var dataSources) || dataSources.ValueKind != JsonValueKind.Array)
        {
            throw new BffUnsanctionedBindingException($"Released application declares no data sources; '{bindingName}' is unsanctioned.");
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
            throw new BffUnsanctionedBindingException($"Data source '{bindingName}' is not part of the released application.");
        }

        // (b) must be referenced by a sanctioned data-source-read action
        if (!IsReferencedByDataSourceReadAction(root, bindingName))
        {
            throw new BffUnsanctionedBindingException($"Data source '{bindingName}' is not referenced by a sanctioned data-source-read action.");
        }

        return binding;
    }

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
        if (!action.TryGetProperty("type", out var type) || type.GetString() != ReleasedActions.DataSourceRead)
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
