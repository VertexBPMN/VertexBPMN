using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Import;

public interface IN8nWorkflowImporter
{
    N8nImportResult Import(string workflowJson);
    N8nImportResult Import(string workflowJson, IReadOnlyList<CredentialMetadata> credentials);
}

public sealed class N8nWorkflowImporter : IN8nWorkflowImporter
{
    private static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    private static readonly XNamespace Vertex = "https://vertexbpmn.dev/schema/1.0";

    public N8nImportResult Import(string workflowJson) => Import(workflowJson, []);

    public N8nImportResult Import(string workflowJson, IReadOnlyList<CredentialMetadata> credentials)
    {
        using var document = JsonDocument.Parse(workflowJson);
        var root = document.RootElement;
        var workflowName = root.TryGetProperty("name", out var name) ? name.GetString() : null;
        var nodes = root.TryGetProperty("nodes", out var nodeArray) && nodeArray.ValueKind == JsonValueKind.Array
            ? nodeArray.EnumerateArray().Select(ParseNode).ToArray()
            : throw new ArgumentException("An n8n workflow must contain a nodes array.", nameof(workflowJson));
        if (nodes.Length == 0) throw new ArgumentException("An n8n workflow must contain at least one node.", nameof(workflowJson));

        var reports = new List<N8nImportReportItem>();
        var ids = nodes.ToDictionary(node => node.Name, node => $"n8n_{Sanitize(node.Name)}", StringComparer.Ordinal);
        var outgoing = ParseConnections(root, ids);
        var process = new XElement(Bpmn + "process", new XAttribute("id", $"n8n_{Sanitize(workflowName ?? "workflow")}"), new XAttribute("name", workflowName ?? "Imported n8n workflow"), new XAttribute("isExecutable", "true"));

        foreach (var node in nodes)
        {
            var element = CreateElement(node, ids[node.Name], reports, credentials);
            foreach (var flow in outgoing.Where(flow => flow.Source == node.Name)) element.Add(new XElement(Bpmn + "outgoing", flow.Id));
            if (!outgoing.Any(flow => flow.Source == node.Name)) element.Add(new XElement(Bpmn + "outgoing", $"end_flow_{Sanitize(node.Name)}"));
            process.Add(element);
        }

        foreach (var flow in outgoing)
        {
            var sequenceFlow = new XElement(Bpmn + "sequenceFlow", new XAttribute("id", flow.Id), new XAttribute("sourceRef", ids[flow.Source]), new XAttribute("targetRef", ids[flow.Target]));
            var source = nodes.Single(node => node.Name == flow.Source);
            if (IsIfNode(source) && flow.Branch == 0 && TryTranslateIfCondition(source.Parameters, out var condition)) sequenceFlow.Add(new XElement(Bpmn + "conditionExpression", new XCData(condition)));
            process.Add(sequenceFlow);
        }

        foreach (var ifNode in nodes.Where(IsIfNode))
        {
            var branches = outgoing.Where(flow => flow.Source == ifNode.Name).OrderBy(flow => flow.Branch).ToArray();
            var gateway = process.Elements(Bpmn + "exclusiveGateway").SingleOrDefault(element => (string?)element.Attribute("id") == ids[ifNode.Name]);
            if (gateway is null) continue;
            if (TryTranslateIfCondition(ifNode.Parameters, out _))
            {
                if (branches.FirstOrDefault(flow => flow.Branch > 0) is { } fallback) gateway.SetAttributeValue("default", fallback.Id);
                reports.Add(new(ifNode.Name, ifNode.Type, N8nImportDisposition.Migrated, "Mapped to an exclusive gateway with a translated condition and default branch."));
            }
            else reports.Add(new(ifNode.Name, ifNode.Type, N8nImportDisposition.NeedsReview, "Mapped to an exclusive gateway, but its condition could not be translated. Configure outgoing BPMN conditions manually."));
        }

        foreach (var terminal in nodes.Where(node => !outgoing.Any(flow => flow.Source == node.Name)))
        {
            var endId = $"end_{Sanitize(terminal.Name)}";
            process.Add(new XElement(Bpmn + "endEvent", new XAttribute("id", endId), new XAttribute("name", $"End {terminal.Name}"), new XElement(Bpmn + "incoming", $"end_flow_{Sanitize(terminal.Name)}")));
            process.Add(new XElement(Bpmn + "sequenceFlow", new XAttribute("id", $"end_flow_{Sanitize(terminal.Name)}"), new XAttribute("sourceRef", ids[terminal.Name]), new XAttribute("targetRef", endId)));
        }

        var definitions = new XDocument(new XElement(Bpmn + "definitions", new XAttribute(XNamespace.Xmlns + "vertex", Vertex), process));
        return new N8nImportResult(definitions.ToString(SaveOptions.DisableFormatting), reports);
    }

    private static N8nNode ParseNode(JsonElement node) => new(
        node.TryGetProperty("name", out var name) ? name.GetString() ?? "unnamed" : "unnamed",
        node.TryGetProperty("type", out var type) ? type.GetString() ?? "unknown" : "unknown",
        node.TryGetProperty("credentials", out var credentials) ? credentials : default,
        node.TryGetProperty("parameters", out var parameters) ? parameters : default);

    private static IReadOnlyList<N8nFlow> ParseConnections(JsonElement root, IReadOnlyDictionary<string, string> ids)
    {
        if (!root.TryGetProperty("connections", out var connections) || connections.ValueKind != JsonValueKind.Object) return [];
        var flows = new List<N8nFlow>(); var index = 0;
        foreach (var source in connections.EnumerateObject())
        {
            if (!ids.ContainsKey(source.Name) || !source.Value.TryGetProperty("main", out var main) || main.ValueKind != JsonValueKind.Array) continue;
            var branchIndex = -1;
            foreach (var branch in main.EnumerateArray())
            {
                branchIndex++;
                if (branch.ValueKind != JsonValueKind.Array) continue;
                foreach (var target in branch.EnumerateArray())
                    if (target.TryGetProperty("node", out var targetName) && targetName.GetString() is { } value && ids.ContainsKey(value)) flows.Add(new N8nFlow(source.Name, value, $"flow_{++index}", branchIndex));
            }
        }
        return flows;
    }

    private static XElement CreateElement(N8nNode node, string id, ICollection<N8nImportReportItem> reports, IReadOnlyList<CredentialMetadata> credentials)
    {
        var type = node.Type.ToLowerInvariant();
        if (type.Contains("webhook"))
        {
            reports.Add(new(node.Name, node.Type, N8nImportDisposition.Migrated, "Mapped to a BPMN message start event."));
            return new XElement(Bpmn + "startEvent", new XAttribute("id", id), new XAttribute("name", node.Name), new XAttribute(Vertex + "trigger", "webhook"));
        }
        if (type.Contains("httprequest"))
        {
            var element = new XElement(Bpmn + "serviceTask", new XAttribute("id", id), new XAttribute("name", node.Name), new XAttribute(Vertex + "connector", "http"));
            var credential = ResolveCredential(node.Credentials, credentials);
            if (credential is not null)
            {
                element.Add(new XAttribute(Vertex + "credentialRef", credential.Id));
                reports.Add(new(node.Name, node.Type, N8nImportDisposition.Migrated, $"Mapped to an HTTP connector and linked to Vertex credential '{credential.Name}'."));
            }
            else if (HasCredentials(node.Credentials)) reports.Add(new(node.Name, node.Type, N8nImportDisposition.NeedsReview, "Mapped to an HTTP connector, but no matching credential exists in the selected tenant. No credential reference was imported."));
            else reports.Add(new(node.Name, node.Type, N8nImportDisposition.Migrated, "Mapped to a service task with an HTTP connector reference."));
            return element;
        }
        if (IsIfNode(node)) return new XElement(Bpmn + "exclusiveGateway", new XAttribute("id", id), new XAttribute("name", node.Name));
        reports.Add(new(node.Name, node.Type, N8nImportDisposition.Unsupported, "Unsupported n8n node retained as a marked service task."));
        return new XElement(Bpmn + "serviceTask", new XAttribute("id", id), new XAttribute("name", node.Name), new XAttribute(Vertex + "n8nType", node.Type), new XAttribute(Vertex + "importStatus", "unsupported"));
    }

    private static bool IsIfNode(N8nNode node) => node.Type.EndsWith(".if", StringComparison.OrdinalIgnoreCase);
    private static bool HasCredentials(JsonElement credentials) => credentials.ValueKind == JsonValueKind.Object && credentials.EnumerateObject().Any();

    private static CredentialMetadata? ResolveCredential(JsonElement credentials, IReadOnlyList<CredentialMetadata> available)
    {
        if (!HasCredentials(credentials)) return null;
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var credential in credentials.EnumerateObject().Select(property => property.Value).Where(value => value.ValueKind == JsonValueKind.Object))
            foreach (var key in new[] { "id", "name" })
                if (credential.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())) candidates.Add(value.GetString()!);
        var matches = available.Where(item => candidates.Contains(item.Id) || candidates.Contains(item.Name)).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool TryTranslateIfCondition(JsonElement parameters, out string expression)
    {
        expression = string.Empty;
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("conditions", out var conditions) || conditions.ValueKind != JsonValueKind.Object) return false;
        foreach (var group in conditions.EnumerateObject().Select(property => property.Value).Where(value => value.ValueKind == JsonValueKind.Array))
        {
            var condition = group.EnumerateArray().FirstOrDefault(value => value.ValueKind == JsonValueKind.Object);
            if (condition.ValueKind != JsonValueKind.Object || !TryString(condition, "value1", out var value1) || !TryString(condition, "operation", out var operation) || !TryString(condition, "value2", out var value2)) continue;
            var variable = NormalizeN8nJsonReference(value1);
            var token = operation.ToLowerInvariant() switch { "equals" or "equal" => "==", "notequals" or "notequal" => "!=", "larger" or "greaterthan" => ">", "smaller" or "lessthan" => "<", "largerequal" or "greaterorequal" => ">=", "smallerequal" or "lessorequal" => "<=", _ => null };
            if (token is null || string.IsNullOrWhiteSpace(variable)) continue;
            expression = $"{variable} {token} {JsonSerializer.Serialize(value2)}";
            return true;
        }
        return false;
    }

    private static bool TryString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(property, out var candidate) || candidate.ValueKind != JsonValueKind.String) return false;
        value = candidate.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string NormalizeN8nJsonReference(string value)
    {
        var trimmed = value.Trim(); const string prefix = "={{ $json."; const string suffix = " }}";
        if (trimmed.StartsWith(prefix, StringComparison.Ordinal) && trimmed.EndsWith(suffix, StringComparison.Ordinal)) return trimmed[prefix.Length..^suffix.Length].Replace('.', '_');
        return trimmed.All(character => char.IsLetterOrDigit(character) || character is '_' or '.') ? trimmed.Replace('.', '_') : string.Empty;
    }

    private static string Sanitize(string value) => new(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
    private sealed record N8nNode(string Name, string Type, JsonElement Credentials, JsonElement Parameters);
    private sealed record N8nFlow(string Source, string Target, string Id, int Branch);
}

public sealed record N8nImportResult(string BpmnXml, IReadOnlyList<N8nImportReportItem> Report);
public sealed record N8nImportReportItem(string NodeName, string NodeType, N8nImportDisposition Disposition, string Message);
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum N8nImportDisposition { Migrated, NeedsReview, Unsupported }
