using System.Text.Json;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Studio.Components.Modeling;

public sealed partial class AppDesignerSurface
{
    private string _endpointKey = "";
    private string _endpointMethod = "GET";
    private string _endpointRoute = "";
    private string _endpointParameters = "";
    private string _endpointPermission = "ReadOnly";
    private int _endpointTimeout = 5;
    private int _endpointRequestBytes = 1024;
    private int _endpointResponseBytes = 4096;
    private string? _endpointAction;
    private string? _endpointError;
    private string _endpointInput = "{\"type\":\"object\",\"properties\":{\"search\":{\"type\":\"string\",\"maxLength\":32}},\"required\":[\"search\"],\"additionalProperties\":false}";
    private string _endpointOutput = "{\"type\":\"object\",\"properties\":{\"items\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"},\"name\":{\"type\":\"string\"}},\"required\":[\"id\",\"name\"],\"additionalProperties\":false}}},\"required\":[\"items\"],\"additionalProperties\":false}";

    private IReadOnlyList<ApplicationEndpointDefinition> Endpoints => ApplicationEndpointMetadata.Read(_document.ToJson());
    private Dictionary<string, ApplicationEndpointActionBinding> EndpointActions()
    {
        using var content = JsonDocument.Parse(_document.ToJson());
        var result = new Dictionary<string, ApplicationEndpointActionBinding>(StringComparer.Ordinal);
        foreach (var page in content.RootElement.GetProperty("pages").EnumerateArray())
        {
            CollectEndpointActions(page, page.GetProperty("id").GetString()!, result);
        }
        return result;
    }

    private static void AddEndpointAction(JsonElement action, string pageId, Dictionary<string, ApplicationEndpointActionBinding> result)
    {
        var type = action.GetProperty("type").GetString();
        if (type is not ("data-source-read" or "start-process") || action.GetProperty("version").GetString() != "1.0.0") { return; }
        if (!action.TryGetProperty(type == "data-source-read" ? "dataSource" : "processDefinitionId", out var target)
            || string.IsNullOrWhiteSpace(target.GetString())) { return; }
        var binding = new ApplicationEndpointActionBinding(pageId, type, "1.0.0", target.GetString()!);
        result[$"{pageId} · {type} · {binding.Target}"] = binding;
    }

    private void ApplyEndpoint()
    {
        try
        {
            if (_endpointAction is null || !EndpointActions().TryGetValue(_endpointAction, out var action))
            { throw new InvalidOperationException("Select an existing application action."); }
            using var input = JsonDocument.Parse(_endpointInput);
            using var output = JsonDocument.Parse(_endpointOutput);
            _document.UpsertEndpoint(new(_endpointKey, _endpointMethod, _endpointRoute,
                _endpointParameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                input.RootElement.Clone(), output.RootElement.Clone(), _endpointPermission,
                new(_endpointTimeout, _endpointRequestBytes, _endpointResponseBytes), action));
            _endpointError = null;
            MarkDirty();
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or VertexBPMN.Domain.Exceptions.ApplicationValidationException)
        { _endpointError = exception.Message; }
    }

    private void EditEndpoint(ApplicationEndpointDefinition endpoint)
    {
        _endpointKey = endpoint.Key;
        _endpointMethod = endpoint.Method;
        _endpointRoute = endpoint.Route;
        _endpointParameters = string.Join(",", endpoint.PathParameters);
        _endpointInput = endpoint.InputSchema.GetRawText();
        _endpointOutput = endpoint.OutputSchema.GetRawText();
        _endpointPermission = endpoint.Permission;
        _endpointTimeout = endpoint.Limits.TimeoutSeconds;
        _endpointRequestBytes = endpoint.Limits.MaxRequestBytes;
        _endpointResponseBytes = endpoint.Limits.MaxResponseBytes;
        _endpointAction = $"{endpoint.Action.PageId} · {endpoint.Action.Type} · {endpoint.Action.Target}";
    }

    private static void CollectEndpointActions(JsonElement node, string pageId, Dictionary<string, ApplicationEndpointActionBinding> result)
    {
        if (node.TryGetProperty("action", out var action)) { AddEndpointAction(action, pageId, result); }
        if (node.TryGetProperty("actions", out var actions))
        { foreach (var item in actions.EnumerateArray()) { AddEndpointAction(item, pageId, result); } }
        foreach (var name in new[] { "components", "children" })
        {
            if (node.TryGetProperty(name, out var children))
            { foreach (var child in children.EnumerateArray()) { CollectEndpointActions(child, pageId, result); } }
        }
    }
}
