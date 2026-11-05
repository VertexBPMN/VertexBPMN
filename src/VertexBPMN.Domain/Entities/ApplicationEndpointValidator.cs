using System.Text.Json;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Domain.Entities;

internal static class ApplicationEndpointValidator
{
    internal static void Validate(string content)
    {
        var endpoints = ApplicationEndpointMetadata.Read(content);
        if (endpoints.Count == 0) { return; }
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        foreach (var endpoint in endpoints) { ApplicationEndpointRoutePolicy.Validate(endpoint); }
        foreach (var binding in endpoints.Select(endpoint => endpoint.Action))
        {
            if (binding is null || !ApplicationComponentCatalog.Actions.Contains(binding.Type)
                || binding.Version != "1.0.0" || !root.TryGetProperty("pages", out var pages)
                || !pages.EnumerateArray().Any(page => page.GetProperty("id").GetString() == binding.PageId
                    && Actions(page).Any(action => Matches(action, binding))))
            { throw new ApplicationValidationException("Endpoint action is not approved in this application."); }
        }
    }

    private static IEnumerable<JsonElement> Actions(JsonElement node)
    {
        if (node.TryGetProperty("actions", out var actions))
        { foreach (var action in actions.EnumerateArray()) { yield return action; } }
        if (node.TryGetProperty("action", out var single)) { yield return single; }
        foreach (var name in new[] { "components", "children" })
        {
            if (!node.TryGetProperty(name, out var children)) { continue; }
            foreach (var child in children.EnumerateArray())
            { foreach (var action in Actions(child)) { yield return action; } }
        }
    }

    private static bool Matches(JsonElement action, ApplicationEndpointActionBinding binding)
    {
        var targetProperty = binding.Type switch
        {
            "data-source-read" => "dataSource",
            "start-process" => "processDefinitionId",
            "claim-task" or "complete-task" => "taskKey",
            _ => null
        };
        return targetProperty is not null && action.GetProperty("type").GetString() == binding.Type
            && action.GetProperty("version").GetString() == binding.Version
            && action.TryGetProperty(targetProperty, out var target) && target.GetString() == binding.Target;
    }
}
