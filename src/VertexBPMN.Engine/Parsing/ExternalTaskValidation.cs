using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Parsing;

/// <summary>Prevents ambiguous task declarations and execution through legacy fallbacks.</summary>
public static class ExternalTaskValidation
{
    public static void ValidateXml(XDocument document)
    {
        XNamespace bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
        foreach (var extension in document.Descendants().Where(e => e.Name.LocalName == "externalTask"))
        {
            var container = extension.Parent;
            var task = container?.Parent;
            if (extension.Name.NamespaceName != VertexBpmnExtensions.NamespaceUri)
                throw new InvalidOperationException("external_task_invalid_namespace");
            if (container?.Name != bpmn + "extensionElements" || task?.Name != bpmn + "serviceTask")
                throw new InvalidOperationException("external_task_invalid_owner");
            if (container.Elements().Count(e => e.Name.LocalName == "externalTask") != 1)
                throw new InvalidOperationException("external_task_duplicate_definition");
            if (task.Attribute("implementation")?.Value is { Length: > 0 } implementation && implementation != "##unspecified"
                || container.Elements().Any(e => e != extension && e.Name.LocalName != "ioMapping"))
                throw new InvalidOperationException("external_task_conflicting_implementation");
            var allowed = new[] { "topic", "agentProfileRef", "maxRetries", "deadlineSeconds" };
            var mappings = container.Elements().Where(child => child.Name.LocalName == "ioMapping").ToArray();
            if (mappings.Length > 1 || mappings.Any(mapping => mapping.Name.NamespaceName != VertexBpmnExtensions.NamespaceUri))
                throw new InvalidOperationException("external_task_invalid_mapping");
            foreach (var mapping in mappings)
            {
                if (mapping.Attributes().Any(attribute => !attribute.IsNamespaceDeclaration))
                    throw new InvalidOperationException("external_task_invalid_mapping");
                var fields = mapping.Elements().ToArray();
                if (fields.Any(field => field.Name.NamespaceName != VertexBpmnExtensions.NamespaceUri
                    || field.Name.LocalName is not ("input" or "output") || field.HasElements)
                    || fields.GroupBy(field => (field.Name.LocalName, (string?)field.Attribute("name"))).Any(group => group.Count() > 1))
                    throw new InvalidOperationException("external_task_invalid_mapping");
                foreach (var field in fields)
                {
                    var valueAttribute = field.Name.LocalName == "input" ? "expression" : "target";
                    if (field.Attributes().Any(attribute => !attribute.IsNamespaceDeclaration
                        && (attribute.Name.NamespaceName.Length != 0 || (attribute.Name.LocalName != "name" && attribute.Name.LocalName != valueAttribute)))
                        || !string.IsNullOrWhiteSpace(field.Value))
                        throw new InvalidOperationException("external_task_invalid_mapping");
                    var name = (string?)field.Attribute("name");
                    var value = (string?)field.Attribute(field.Name.LocalName == "input" ? "expression" : "target");
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value)
                        || (field.Name.LocalName == "output" && (name != "result"
                            || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '_')))))
                        throw new InvalidOperationException("external_task_invalid_mapping");
                }
            }
            if (extension.HasElements || !string.IsNullOrWhiteSpace(extension.Value) || extension.Attributes().Any(a => !a.IsNamespaceDeclaration && (a.Name.NamespaceName.Length != 0 || !allowed.Contains(a.Name.LocalName))))
                throw new InvalidOperationException("external_task_invalid_configuration");
            var attributes = new Dictionary<string, string> { ["vertex:externalTask"] = "true" };
            foreach (var attribute in extension.Attributes().Where(a => !a.IsNamespaceDeclaration))
                attributes["vertex:externalTask." + attribute.Name.LocalName] = attribute.Value;
            _ = ExternalTaskDefinition.FromAttributes(attributes);
        }
    }

    public static void RejectUnsupported(BpmnModel model, Func<string, BpmnModel?>? resolve = null)
    {
        var pending = new Stack<BpmnModel>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        pending.Push(model);
        while (pending.TryPop(out var current))
        {
            if (!visited.Add(current.ProcessId)) continue;
            if (visited.Count > 64) throw new InvalidOperationException("external_task_call_graph_limit");
            if (current.Tasks.Any(task => task.ExternalTask is not null))
                throw new InvalidOperationException("external_task_engine_unsupported");
            foreach (var call in current.Tasks.Where(task => task.Type == "callActivity"))
                if (call.Attributes?.GetValueOrDefault("calledElement") is { Length: > 0 } key && resolve?.Invoke(key) is { } child)
                    pending.Push(child);
        }
    }
}
