using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace VertexBPMN.Application.Import;

public interface IN8nWorkflowImporter
{
    N8nImportResult Import(string workflowJson);
}

public sealed class N8nWorkflowImporter : IN8nWorkflowImporter
{
    private static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    private static readonly XNamespace Vertex = "https://vertexbpmn.dev/schema/1.0";

    public N8nImportResult Import(string workflowJson)
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
            var element = CreateElement(node, ids[node.Name], reports);
            foreach (var flow in outgoing.Where(flow => flow.Source == node.Name))
                element.Add(new XElement(Bpmn + "outgoing", flow.Id));
            if (!outgoing.Any(flow => flow.Source == node.Name))
                element.Add(new XElement(Bpmn + "outgoing", $"end_flow_{Sanitize(node.Name)}"));
            process.Add(element);
        }

        foreach (var flow in outgoing)
            process.Add(new XElement(Bpmn + "sequenceFlow", new XAttribute("id", flow.Id), new XAttribute("sourceRef", ids[flow.Source]), new XAttribute("targetRef", ids[flow.Target])));

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
        node.TryGetProperty("credentials", out var credentials) ? credentials : default);

    private static IReadOnlyList<N8nFlow> ParseConnections(JsonElement root, IReadOnlyDictionary<string, string> ids)
    {
        if (!root.TryGetProperty("connections", out var connections) || connections.ValueKind != JsonValueKind.Object) return [];
        var flows = new List<N8nFlow>(); var index = 0;
        foreach (var source in connections.EnumerateObject())
        {
            if (!ids.ContainsKey(source.Name) || !source.Value.TryGetProperty("main", out var main) || main.ValueKind != JsonValueKind.Array) continue;
            foreach (var branch in main.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Array))
            foreach (var target in branch.EnumerateArray())
            {
                if (!target.TryGetProperty("node", out var targetName) || targetName.GetString() is not { } value || !ids.ContainsKey(value)) continue;
                flows.Add(new N8nFlow(source.Name, value, $"flow_{++index}"));
            }
        }
        return flows;
    }

    private static XElement CreateElement(N8nNode node, string id, ICollection<N8nImportReportItem> reports)
    {
        var type = node.Type.ToLowerInvariant();
        if (type.Contains("webhook"))
        {
            reports.Add(new(node.Name, node.Type, N8nImportDisposition.Migrated, "Mapped to a BPMN message start event."));
            return new XElement(Bpmn + "startEvent", new XAttribute("id", id), new XAttribute("name", node.Name), new XAttribute(Vertex + "trigger", "webhook"));
        }
        if (type.Contains("httprequest"))
        {
            reports.Add(new(node.Name, node.Type, N8nImportDisposition.Migrated, "Mapped to a service task with an HTTP connector reference."));
            var element = new XElement(Bpmn + "serviceTask", new XAttribute("id", id), new XAttribute("name", node.Name), new XAttribute(Vertex + "connector", "http"));
            if (node.Credentials.ValueKind == JsonValueKind.Object) element.Add(new XAttribute(Vertex + "credentialRef", "TODO: map n8n credential"));
            return element;
        }
        if (type.EndsWith(".if", StringComparison.Ordinal))
        {
            reports.Add(new(node.Name, node.Type, N8nImportDisposition.NeedsReview, "Mapped to an exclusive gateway; n8n conditions require review."));
            return new XElement(Bpmn + "exclusiveGateway", new XAttribute("id", id), new XAttribute("name", node.Name), new XAttribute(Vertex + "expression", "TODO: translate n8n IF expression"));
        }
        reports.Add(new(node.Name, node.Type, N8nImportDisposition.Unsupported, "Unsupported n8n node retained as a marked service task."));
        return new XElement(Bpmn + "serviceTask", new XAttribute("id", id), new XAttribute("name", node.Name), new XAttribute(Vertex + "n8nType", node.Type), new XAttribute(Vertex + "importStatus", "unsupported"));
    }

    private static string Sanitize(string value) => new(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
    private sealed record N8nNode(string Name, string Type, JsonElement Credentials);
    private sealed record N8nFlow(string Source, string Target, string Id);
}

public sealed record N8nImportResult(string BpmnXml, IReadOnlyList<N8nImportReportItem> Report);
public sealed record N8nImportReportItem(string NodeName, string NodeType, N8nImportDisposition Disposition, string Message);
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum N8nImportDisposition { Migrated, NeedsReview, Unsupported }
