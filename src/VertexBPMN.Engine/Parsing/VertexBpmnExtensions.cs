using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Parsing;

/// <summary>
/// Canonical Vertex BPMN extension namespace plus legacy aliases used by sequence-flow priority.
/// Typed vertex:* fields are a projection on top of strict raw extension clones.
/// </summary>
public static class VertexBpmnExtensions
{
    public const string NamespaceUri = "https://vertexbpmn.io/schema/bpmn/1.0";
    public const string LegacyNamespaceUri = "http://vertexbpmn.io/schema/1.0";
    public const string LegacyBpmnNamespaceUri = "http://vertexbpmn.io/schema/1.0/bpmn";
    public const string Prefix = "vertex";

    public static bool IsVertexNamespace(string? nsUri) =>
        nsUri == NamespaceUri || nsUri == LegacyNamespaceUri || nsUri == LegacyBpmnNamespaceUri;

    public static void Flatten(XElement child, Dictionary<string, string> bucket)
    {
        var local = child.Name.LocalName;
        switch (local)
        {
            case "connector":
                CopyAttributes(child, bucket, "vertex:connector");
                break;
            case "retryPolicy":
                CopyAttributes(child, bucket, "vertex:retryPolicy");
                break;
            case "ioMapping":
                foreach (var input in child.Elements().Where(e => e.Name.LocalName == "input"))
                {
                    var name = input.Attribute("name")?.Value;
                    var expression = input.Attribute("expression")?.Value;
                    if (!string.IsNullOrEmpty(name) && expression != null)
                        bucket[$"vertex:ioMapping.input.{name}"] = expression;
                }
                foreach (var output in child.Elements().Where(e => e.Name.LocalName == "output"))
                {
                    var name = output.Attribute("name")?.Value;
                    var target = output.Attribute("target")?.Value;
                    if (!string.IsNullOrEmpty(name) && target != null)
                        bucket[$"vertex:ioMapping.output.{name}"] = target;
                }
                break;
            case "webhook":
                CopyAttributes(child, bucket, "vertex:webhook");
                break;
            case "trigger":
                CopyAttributes(child, bucket, "vertex:trigger");
                break;
            case "decision":
                CopyAttributes(child, bucket, "vertex:decision");
                break;
            case "form":
                CopyAttributes(child, bucket, "vertex:form");
                break;
            case "assignment":
                CopyAttributes(child, bucket, "vertex:assignment");
                break;
            case "case":
                CopyAttributes(child, bucket, "vertex:case");
                break;
            case "credential":
                CopyAttributes(child, bucket, "vertex:credential");
                break;
        }
    }

    public static void Validate(BpmnModel model, List<ValidationDiagnostic> list)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (ownerId, root) in EnumerateExtensionRoots(model))
        {
            foreach (var child in root.Elements())
            {
                if (!IsVertexNamespace(child.Name.NamespaceName))
                    continue;
                seen.Add(ownerId + ":" + child.Name.LocalName);
                ValidateElement(ownerId, child, list);
            }
        }

        // Fallback when strict raw clones were not captured (normalized parse).
        if (model.Tasks != null)
            ValidateFromAttributes(model.Tasks.Select(t => (t.Id, t.Attributes)), list, seen);
        if (model.Events != null)
            ValidateFromAttributes(model.Events.Select(e => (e.Id, e.Attributes)), list, seen);
        if (model.Gateways != null)
            ValidateFromAttributes(model.Gateways.Select(g => (g.Id, g.ExtensionAttributes)), list, seen);
        if (model.Subprocesses != null)
            ValidateFromAttributes(model.Subprocesses.Select(s => (s.Id, s.ExtensionAttributes)), list, seen);
        if (model.SequenceFlows != null)
            ValidateFromAttributes(model.SequenceFlows.Select(f => (f.Id, f.ExtensionAttributes)), list, seen);
    }

    private static void ValidateFromAttributes(
        IEnumerable<(string Id, Dictionary<string, string>? Attributes)> owners,
        List<ValidationDiagnostic> list,
        HashSet<string> seen)
    {
        foreach (var (id, attributes) in owners)
        {
            if (string.IsNullOrEmpty(id) || attributes == null || attributes.Count == 0)
                continue;
            ValidateAttributeGroup(id, attributes, "vertex:connector", "type", "VEN-VERTEX-CONNECTOR-TYPE",
                $"vertex:connector on '{id}' is missing required type", list, seen, "connector");
            ValidateAttributeGroup(id, attributes, "vertex:connector", "operationId", "VEN-VERTEX-CONNECTOR-OPERATION",
                $"vertex:connector on '{id}' is missing required operationId", list, seen, "connector");
            ValidateAttributeGroup(id, attributes, "vertex:webhook", "path", "VEN-VERTEX-WEBHOOK-PATH",
                $"vertex:webhook on '{id}' is missing required path", list, seen, "webhook");
            ValidateAttributeGroup(id, attributes, "vertex:trigger", "type", "VEN-VERTEX-TRIGGER-TYPE",
                $"vertex:trigger on '{id}' is missing required type", list, seen, "trigger");
            ValidateAttributeGroup(id, attributes, "vertex:trigger", "processDefinitionKey", "VEN-VERTEX-TRIGGER-PROCESS-KEY",
                $"vertex:trigger on '{id}' is missing required processDefinitionKey", list, seen, "trigger");
            ValidateAttributeGroup(id, attributes, "vertex:decision", "decisionRef", "VEN-VERTEX-DECISION-REF",
                $"vertex:decision on '{id}' is missing required decisionRef", list, seen, "decision");
            ValidateAttributeGroup(id, attributes, "vertex:credential", "id", "VEN-VERTEX-CREDENTIAL-ID",
                $"vertex:credential on '{id}' is missing required id", list, seen, "credential");
            ValidateAttributeGroup(id, attributes, "vertex:credential", "kind", "VEN-VERTEX-CREDENTIAL-KIND",
                $"vertex:credential on '{id}' is missing required kind", list, seen, "credential");
        }
    }

    private static void ValidateAttributeGroup(
        string ownerId,
        Dictionary<string, string> attributes,
        string prefix,
        string requiredLocal,
        string code,
        string message,
        List<ValidationDiagnostic> list,
        HashSet<string> seen,
        string localName)
    {
        if (seen.Contains(ownerId + ":" + localName))
            return;
        var hasGroup = attributes.Keys.Any(k =>
            k.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase) ||
            k.Equals(prefix, StringComparison.OrdinalIgnoreCase));
        if (!hasGroup)
            return;
        if (attributes.TryGetValue(prefix + "." + requiredLocal, out var value) && !string.IsNullOrWhiteSpace(value))
            return;
        if (list.Exists(d => d.Code == code && d.ElementId == ownerId))
            return;
        list.Add(new ValidationDiagnostic(
            Code: code,
            Severity: ValidationSeverity.Error,
            Message: message,
            ElementId: ownerId,
            Category: "Vertex"));
    }

    private static void ValidateElement(string ownerId, XElement child, List<ValidationDiagnostic> list)
    {
        switch (child.Name.LocalName)
        {
            case "connector":
                Require(child, "type", ownerId, "VEN-VERTEX-CONNECTOR-TYPE",
                    $"vertex:connector on '{ownerId}' is missing required type", list);
                Require(child, "operationId", ownerId, "VEN-VERTEX-CONNECTOR-OPERATION",
                    $"vertex:connector on '{ownerId}' is missing required operationId", list);
                break;
            case "webhook":
                Require(child, "path", ownerId, "VEN-VERTEX-WEBHOOK-PATH",
                    $"vertex:webhook on '{ownerId}' is missing required path", list);
                break;
            case "trigger":
                Require(child, "type", ownerId, "VEN-VERTEX-TRIGGER-TYPE",
                    $"vertex:trigger on '{ownerId}' is missing required type", list);
                Require(child, "processDefinitionKey", ownerId, "VEN-VERTEX-TRIGGER-PROCESS-KEY",
                    $"vertex:trigger on '{ownerId}' is missing required processDefinitionKey", list);
                break;
            case "decision":
                Require(child, "decisionRef", ownerId, "VEN-VERTEX-DECISION-REF",
                    $"vertex:decision on '{ownerId}' is missing required decisionRef", list);
                break;
            case "credential":
                Require(child, "id", ownerId, "VEN-VERTEX-CREDENTIAL-ID",
                    $"vertex:credential on '{ownerId}' is missing required id", list);
                Require(child, "kind", ownerId, "VEN-VERTEX-CREDENTIAL-KIND",
                    $"vertex:credential on '{ownerId}' is missing required kind", list);
                break;
        }
    }

    private static void Require(XElement element, string attribute, string ownerId, string code, string message, List<ValidationDiagnostic> list)
    {
        var value = element.Attribute(attribute)?.Value;
        if (!string.IsNullOrWhiteSpace(value))
            return;
        if (list.Exists(d => d.Code == code && d.ElementId == ownerId))
            return;
        list.Add(new ValidationDiagnostic(
            Code: code,
            Severity: ValidationSeverity.Error,
            Message: message,
            ElementId: ownerId,
            Category: "Vertex"));
    }

    private static IEnumerable<(string OwnerId, XElement Root)> EnumerateExtensionRoots(BpmnModel model)
    {
        var raw = model.RawMetadata?.RawExtensionElements;
        if (raw == null)
            yield break;
        foreach (var kv in raw)
        {
            if (string.IsNullOrEmpty(kv.Key) || kv.Value == null)
                continue;
            yield return (kv.Key, kv.Value);
        }
    }

    private static void CopyAttributes(XElement child, Dictionary<string, string> bucket, string prefix)
    {
        foreach (var attr in child.Attributes())
        {
            if (attr.IsNamespaceDeclaration)
                continue;
            if (attr.Value.Length == 0)
                continue;
            bucket[$"{prefix}.{attr.Name.LocalName}"] = attr.Value;
        }
    }
}
