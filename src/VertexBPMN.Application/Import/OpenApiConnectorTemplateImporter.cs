using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Import;

public interface IOpenApiConnectorTemplateImporter
{
    OpenApiImportResult Import(string openApiJsonOrYaml, string tenantId);
}

public sealed record OpenApiImportResult(
    IReadOnlyList<ConnectorTemplateWriteRequest> Templates,
    IReadOnlyList<OpenApiImportReportItem> Report);

public sealed record OpenApiImportReportItem(string OperationId, N8nImportDisposition Disposition, string Message);

/// <summary>
/// Turns an OpenAPI 3.x JSON spec into reusable <c>ConnectorTemplateWriteRequest</c> entries
/// (Runtime = "http") that the Studio palette and <c>HttpConnectorExecutor</c> can consume
/// without writing a dedicated <see cref="VertexBPMN.Application.Connectors.IConnectorExecutor"/>.
/// JSON-only in this phase; YAML support is a separate ticket.
/// </summary>
public sealed class OpenApiConnectorTemplateImporter : IOpenApiConnectorTemplateImporter
{
    private static readonly string[] HttpMethods = ["get", "post", "put", "patch", "delete", "head", "options"];

    public OpenApiImportResult Import(string openApiJsonOrYaml, string tenantId)
    {
        var trimmed = openApiJsonOrYaml.Trim();
        if (!trimmed.StartsWith('{'))
            throw new ArgumentException("Nur JSON-OpenAPI-Specs werden unterstützt; YAML-Support ist ein separates Ticket.");

        using var doc = JsonDocument.Parse(trimmed, new JsonDocumentOptions { AllowTrailingCommas = true });
        var root = doc.RootElement;
        if (!root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("OpenAPI-Spec hat keinen 'paths'-Abschnitt.");

        var serverUrl = ReadServerUrl(root);
        var templates = new List<ConnectorTemplateWriteRequest>();
        var report = new List<OpenApiImportReportItem>();

        foreach (var pathProp in paths.EnumerateObject())
        {
            var path = pathProp.Name;
            if (pathProp.Value.ValueKind != JsonValueKind.Object) continue;
            foreach (var methodProp in pathProp.Value.EnumerateObject())
            {
                var method = methodProp.Name.ToLowerInvariant();
                if (!HttpMethods.Contains(method) || methodProp.Value.ValueKind != JsonValueKind.Object) continue;
                var op = methodProp.Value;

                if (!op.TryGetProperty("operationId", out var operationIdProp) || string.IsNullOrWhiteSpace(operationIdProp.GetString()))
                {
                    report.Add(new($"{method.ToUpperInvariant()} {path}", N8nImportDisposition.Unsupported, "operationId fehlt"));
                    continue;
                }
                var operationId = operationIdProp.GetString()!;

                var props = new List<ConnectorTemplateProperty>();
                var notes = new List<string>();
                var needsReview = false;

                CollectParameters(op, props, ref needsReview, notes);
                CollectRequestBody(op, props, ref needsReview, notes);
                var auth = ResolveAuth(op, root, props, ref needsReview, notes);

                var finalProps = new List<ConnectorTemplateProperty>(props)
                {
                    new("vertex:connector.method", "string", true, method, null),
                    new("vertex:connector.endpoint", "string", true, serverUrl + path, null)
                };
                if (auth != null)
                    finalProps.Add(new("vertex:connector.authScheme", "string", true, auth, null));

                templates.Add(new ConnectorTemplateWriteRequest(
                    operationId, "openapi-import", ["serviceTask"], "http", null, finalProps));

                report.Add(new(operationId,
                    needsReview ? N8nImportDisposition.NeedsReview : N8nImportDisposition.Migrated,
                    needsReview ? string.Join("; ", notes) : $"Gemappt von {method.ToUpperInvariant()} {path}"));
            }
        }

        return new OpenApiImportResult(templates, report);
    }

    private static string ReadServerUrl(JsonElement root)
    {
        if (root.TryGetProperty("servers", out var servers) && servers.ValueKind == JsonValueKind.Array)
        {
            foreach (var server in servers.EnumerateArray())
            {
                if (server.ValueKind == JsonValueKind.Object &&
                    server.TryGetProperty("url", out var url) &&
                    !string.IsNullOrWhiteSpace(url.GetString()))
                {
                    return url.GetString()!.TrimEnd('/');
                }
            }
        }
        return string.Empty;
    }

    private static void CollectParameters(
        JsonElement operation, List<ConnectorTemplateProperty> props, ref bool needsReview, List<string> notes)
    {
        if (!operation.TryGetProperty("parameters", out var parameters) || parameters.ValueKind != JsonValueKind.Array) return;
        foreach (var parameter in parameters.EnumerateArray())
        {
            if (parameter.ValueKind != JsonValueKind.Object) continue;
            var name = parameter.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            var location = parameter.TryGetProperty("in", out var inElement) ? inElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) || location is not ("path" or "query" or "header")) continue;

            var type = "string";
            if (parameter.TryGetProperty("schema", out var schema) && schema.ValueKind == JsonValueKind.Object)
                type = MapType(schema, ref needsReview, notes);
            props.Add(new ConnectorTemplateProperty(name!, type, Required: location == "path"));
        }
    }

    private static void CollectRequestBody(
        JsonElement operation, List<ConnectorTemplateProperty> props, ref bool needsReview, List<string> notes)
    {
        if (!operation.TryGetProperty("requestBody", out var requestBody) || requestBody.ValueKind != JsonValueKind.Object) return;
        if (!requestBody.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object) return;
        foreach (var media in content.EnumerateObject())
        {
            if (!media.Name.Contains("json", StringComparison.OrdinalIgnoreCase) || media.Value.ValueKind != JsonValueKind.Object) continue;
            if (!media.Value.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Object) continue;
            props.Add(new ConnectorTemplateProperty("body", MapType(schema, ref needsReview, notes), Required: true));
            break;
        }
    }

    private static string MapType(JsonElement schema, ref bool needsReview, List<string> notes)
    {
        if (schema.TryGetProperty("type", out var typeElement))
        {
            switch (typeElement.GetString())
            {
                case "string": return "string";
                case "integer":
                case "number": return "number";
                case "boolean": return "boolean";
            }
        }

        needsReview = true;
        if (!notes.Contains("Komplexes Schema wurde auf Freitext reduziert, manuell prüfen"))
            notes.Add("Komplexes Schema wurde auf Freitext reduziert, manuell prüfen");
        return "string";
    }

    private static string? ResolveAuth(
        JsonElement operation, JsonElement root, List<ConnectorTemplateProperty> props, ref bool needsReview, List<string> notes)
    {
        var schemeNames = ResolveSecuritySchemeNames(operation, root);
        string? wiredScheme = null;
        foreach (var schemeName in schemeNames)
        {
            var scheme = FindScheme(root, schemeName);
            if (scheme.ValueKind != JsonValueKind.Object) continue;
            if (!scheme.TryGetProperty("type", out var typeElement)) continue;

            switch (typeElement.GetString())
            {
                case "apiKey":
                    var name = scheme.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "apiKey";
                    var keyName = name ?? "apiKey";
                    if (!props.Any(p => p.Key == keyName))
                        props.Add(new ConnectorTemplateProperty(keyName, "string", true, null, null));
                    wiredScheme ??= "ApiKey";
                    break;
                case "http" when scheme.TryGetProperty("scheme", out var httpScheme) && string.Equals(httpScheme.GetString(), "bearer", StringComparison.OrdinalIgnoreCase):
                    wiredScheme ??= "Bearer";
                    break;
                case "oauth2":
                    needsReview = true;
                    if (!notes.Contains("OAuth2-Security-Scheme erkannt, Credential muss nach Phase 2 (OAuth2-Flow) manuell verknüpft werden."))
                        notes.Add("OAuth2-Security-Scheme erkannt, Credential muss nach Phase 2 (OAuth2-Flow) manuell verknüpft werden.");
                    break;
            }
        }
        return wiredScheme;
    }

    private static List<string> ResolveSecuritySchemeNames(JsonElement operation, JsonElement root)
    {
        if (operation.TryGetProperty("security", out var security) && security.ValueKind == JsonValueKind.Array)
        {
            var names = FlattenSecurity(security);
            if (names.Count > 0) return names;
        }
        if (root.TryGetProperty("security", out var globalSecurity) && globalSecurity.ValueKind == JsonValueKind.Array)
            return FlattenSecurity(globalSecurity);
        return [];
    }

    private static List<string> FlattenSecurity(JsonElement security)
        => security.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .SelectMany(item => item.EnumerateObject())
            .Select(pair => pair.Name)
            .ToList();

    private static JsonElement FindScheme(JsonElement root, string schemeName)
    {
        if (!root.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Object) return default;
        if (!components.TryGetProperty("securitySchemes", out var schemes) || schemes.ValueKind != JsonValueKind.Object) return default;
        return schemes.TryGetProperty(schemeName, out var scheme) ? scheme : default;
    }
}
