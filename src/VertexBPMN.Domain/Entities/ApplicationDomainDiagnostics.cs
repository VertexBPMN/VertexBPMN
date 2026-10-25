using System.Text.Json;

namespace VertexBPMN.Domain.Entities;

public static class ApplicationDomainDiagnostics
{
    public static string Validate(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var diagnostics = new List<string>();
        var sources = root.TryGetProperty("dataSources", out var dataSources)
            ? dataSources.EnumerateArray().Select(x => x.GetProperty("key").GetString()!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        if (root.TryGetProperty("pages", out var pages))
        {
            foreach (var page in pages.EnumerateArray())
            {
                if (page.TryGetProperty("actions", out var actions))
                {
                    foreach (var action in actions.EnumerateArray()) { ValidateAction(root, action, diagnostics); }
                }
                if (!page.TryGetProperty("components", out var components)) { continue; }
                foreach (var component in components.EnumerateArray())
                {
                    if (component.TryGetProperty("action", out var action)) { ValidateAction(root, action, diagnostics); }
                    if (component.TryGetProperty("dataSource", out var source) && !sources.Contains(source.GetString()!))
                    { diagnostics.Add("UNKNOWN_DATA_SOURCE"); }
                }
            }
        }
        return JsonSerializer.Serialize(diagnostics);
    }

    private static void ValidateAction(JsonElement root, JsonElement action, List<string> diagnostics)
    {
        if (action.GetProperty("type").GetString() == "start-process")
        {
            var id = action.TryGetProperty("processDefinitionId", out var processId) ? processId.GetString() : null;
            if (id is null || !root.TryGetProperty("references", out var references)
                || !references.EnumerateArray().Any(x =>
                {
                    if (x.GetProperty("type").GetString() != "process")
                    {
                        return false;
                    }
                    if (x.GetProperty("id").GetString() == id)
                    {
                        return true;
                    }
                    // The start-process action references the stable process KEY (the runtime
                    // starts by key), while references[].id is the internal ProcessDefinition
                    // GUID. A reference may declare its key so the action resolves to it.
                    return x.TryGetProperty("key", out var key) && key.GetString() == id;
                }))
            { diagnostics.Add("UNKNOWN_PROCESS"); }
        }
    }
}
