using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Serialization;

/// <summary>
///  Deterministic normalization serializer for runtime deployment artifacts.
/// Produces canonical, deterministic XML output optimized for caching and deployment.
/// </summary>
public class NormalizedProjectionSerializer
{
    private readonly BpmnParserOptions _options;

    public NormalizedProjectionSerializer(BpmnParserOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Serializes a BPMN model using normalized projection approach.
    /// Produces deterministic, canonical XML suitable for runtime deployment.
    /// </summary>
    public string Serialize(BpmnModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var ns = XNamespace.Get("http://www.omg.org/spec/BPMN/20100524/MODEL");
        
        // Phase 8: Build namespace declarations in deterministic order
        var namespaceDeclarations = new SortedDictionary<string, string>
        {
            [""] = "http://www.omg.org/spec/BPMN/20100524/MODEL"
        };

        // Collect vendor namespaces that are actually used
        if (HasVendorExtensions(model))
        {
            CollectUsedNamespaces(model, namespaceDeclarations);
        }

        // Create root definitions element with canonical namespace declarations
        var definitions = new XElement(ns + "definitions");
        foreach (var nsDecl in namespaceDeclarations)
        {
            if (string.IsNullOrEmpty(nsDecl.Key))
                definitions.Add(new XAttribute("xmlns", nsDecl.Value));
            else
                definitions.Add(new XAttribute(XNamespace.Xmlns + nsDecl.Key, nsDecl.Value));
        }

        // Create process element
        var process = new XElement(ns + "process",
            new XAttribute("id", model.ProcessId));

        // Phase 8: Add elements in canonical order for deterministic output
        if (_options.EnableCanonicalSort)
        {
            AddElementsInCanonicalOrder(process, model, ns, namespaceDeclarations);
        }
        else
        {
            AddElementsInParseOrder(process, model, ns, namespaceDeclarations);
        }

        definitions.Add(process);

        // Add global elements (messages, signals, errors, escalations) if present
        AddGlobalElements(definitions, model, ns);

        return FormatXml(definitions);
    }

    private void CollectUsedNamespaces(BpmnModel model, SortedDictionary<string, string> namespaceDeclarations)
    {
        var knownNamespaces = new Dictionary<string, string>
        {
            ["camunda"] = "http://camunda.org/schema/1.0/bpmn",
            ["zeebe"] = "http://zeebe.io/schema/zeebe/1.0",
            ["flowable"] = "http://flowable.org/bpmn",
            ["activiti"] = "http://activiti.org/bpmn"
        };

        // Check all tasks for vendor extensions - Fixed: Use Attributes instead of Extensions
        foreach (var task in model.Tasks)
        {
            if (task.Attributes != null)
            {
                foreach (var attr in task.Attributes.Keys)
                {
                    if (attr.Contains(':'))
                    {
                        var prefix = attr.Split(':')[0];
                        if (knownNamespaces.TryGetValue(prefix, out var namespaceUri))
                        {
                            namespaceDeclarations[prefix] = namespaceUri;
                        }
                    }
                }
            }
        }

        // Check events for vendor extensions
        foreach (var evt in model.Events)
        {
            if (evt.Attributes != null)
            {
                foreach (var attr in evt.Attributes.Keys)
                {
                    if (attr.Contains(':'))
                    {
                        var prefix = attr.Split(':')[0];
                        if (knownNamespaces.TryGetValue(prefix, out var namespaceUri))
                        {
                            namespaceDeclarations[prefix] = namespaceUri;
                        }
                    }
                }
            }
        }

        // Check sequence flows for priority attributes with vendor namespaces
        if (model.RawMetadata?.PriorityAttributeNamespace != null)
        {
            foreach (var ns in model.RawMetadata.PriorityAttributeNamespace.Values)
            {
                var foundPrefix = knownNamespaces.FirstOrDefault(kv => kv.Value == ns).Key;
                if (!string.IsNullOrEmpty(foundPrefix))
                {
                    namespaceDeclarations[foundPrefix] = ns;
                }
            }
        }

        // Check sequence flows for extensions
        foreach (var flow in model.SequenceFlows)
        {
            if (flow.Attributes != null)
            {
                foreach (var attr in flow.Attributes.Keys)
                {
                    if (attr.Contains(':'))
                    {
                        var prefix = attr.Split(':')[0];
                        if (knownNamespaces.TryGetValue(prefix, out var namespaceUri))
                        {
                            namespaceDeclarations[prefix] = namespaceUri;
                        }
                    }
                }
            }
        }
    }

    private void AddElementsInCanonicalOrder(XElement process, BpmnModel model, XNamespace ns, 
        SortedDictionary<string, string> namespaceDeclarations)
    {
        // Phase 8: Canonical order - events first, then activities, then gateways, then flows
        
        // 1. Events (start, intermediate, end, boundary - ordered by type then id)
        var sortedEvents = model.Events
            .OrderBy(GetEventSortOrder)
            .ThenBy(e => e.Id, StringComparer.Ordinal);

        foreach (var evt in sortedEvents)
        {
            process.Add(CreateEventElement(evt, ns, namespaceDeclarations));
        }

        // 2. Activities (tasks, subprocesses - ordered by type then id) 
        var sortedTasks = model.Tasks
            .OrderBy(t => t.Type, StringComparer.Ordinal)
            .ThenBy(t => t.Id, StringComparer.Ordinal);

        foreach (var task in sortedTasks)
        {
            process.Add(CreateTaskElement(task, ns, namespaceDeclarations));
        }

        var sortedSubprocesses = model.Subprocesses
            .OrderBy(s => s.Id, StringComparer.Ordinal);

        foreach (var subprocess in sortedSubprocesses)
        {
            process.Add(CreateSubprocessElement(subprocess, ns, namespaceDeclarations));
        }

        // 3. Gateways (ordered by type then id)
        var sortedGateways = model.Gateways
            .OrderBy(g => g.Type, StringComparer.Ordinal)
            .ThenBy(g => g.Id, StringComparer.Ordinal);

        foreach (var gateway in sortedGateways)
        {
            process.Add(CreateGatewayElement(gateway, ns));
        }

        // 4. Sequence flows (ordered by id)
        var sortedFlows = model.SequenceFlows
            .OrderBy(f => f.Id, StringComparer.Ordinal);

        foreach (var flow in sortedFlows)
        {
            process.Add(CreateSequenceFlowElement(flow, ns, namespaceDeclarations));
        }

        // 5. Data elements if present
        AddDataElements(process, model, ns);
    }

    private void AddElementsInParseOrder(XElement process, BpmnModel model, XNamespace ns,
        SortedDictionary<string, string> namespaceDeclarations)
    {
        // Non-canonical: preserve relative order but still deterministic within each type
        foreach (var evt in model.Events)
        {
            process.Add(CreateEventElement(evt, ns, namespaceDeclarations));
        }

        foreach (var task in model.Tasks)
        {
            process.Add(CreateTaskElement(task, ns, namespaceDeclarations));
        }

        foreach (var subprocess in model.Subprocesses)
        {
            process.Add(CreateSubprocessElement(subprocess, ns, namespaceDeclarations));
        }

        foreach (var gateway in model.Gateways)
        {
            process.Add(CreateGatewayElement(gateway, ns));
        }

        foreach (var flow in model.SequenceFlows)
        {
            process.Add(CreateSequenceFlowElement(flow, ns, namespaceDeclarations));
        }

        AddDataElements(process, model, ns);
    }

    private static int GetEventSortOrder(BpmnEvent evt)
    {
        return evt.Type switch
        {
            "startEvent" => 1,
            "intermediateCatchEvent" => 2,
            "intermediateThrowEvent" => 3,
            "boundaryEvent" => 4,
            "endEvent" => 5,
            _ => 999
        };
    }

    private XElement CreateEventElement(BpmnEvent evt, XNamespace ns, SortedDictionary<string, string> namespaceDeclarations)
    {
        var element = new XElement(ns + evt.Type, new XAttribute("id", evt.Id));
        
        // Add name if present - Fixed: Get name from Attributes
        var name = GetEventName(evt);
        if (!string.IsNullOrEmpty(name))
        {
            element.Add(new XAttribute("name", name));
        }

        // Add boundary event specific attributes
        if (evt.Type == "boundaryEvent")
        {
            var attachedToRef = GetAttachedToRef(evt);
            if (!string.IsNullOrEmpty(attachedToRef))
            {
                element.Add(new XAttribute("attachedToRef", attachedToRef));
            }
        }

        // Add event definitions
        AddEventDefinitions(element, evt, ns);

        return element;
    }

    private XElement CreateTaskElement(BpmnTask task, XNamespace ns, SortedDictionary<string, string> namespaceDeclarations)
    {
        var element = new XElement(ns + task.Type, new XAttribute("id", task.Id));

        if (!string.IsNullOrEmpty(task.Name))
        {
            element.Add(new XAttribute("name", task.Name));
        }

        // Add vendor extensions in normalized form - Fixed: Use task.Attributes
        AddVendorExtensions(element, task.Attributes, namespaceDeclarations);

        return element;
    }

    private XElement CreateSubprocessElement(BpmnSubprocess subprocess, XNamespace ns, SortedDictionary<string, string> namespaceDeclarations)
    {
        var element = new XElement(ns + "subProcess", new XAttribute("id", subprocess.Id));

        if (subprocess.IsEventSubprocess)
        {
            element.Add(new XAttribute("triggeredByEvent", "true"));
        }

        if (subprocess.IsTransaction)
        {
            element.Add(new XAttribute("transaction", "true"));
        }

        // Add extensions if present
        AddVendorExtensions(element, subprocess.Attributes, namespaceDeclarations);

        return element;
    }

    private static XElement CreateGatewayElement(BpmnGateway gateway, XNamespace ns)
    {
        var element = new XElement(ns + gateway.Type, new XAttribute("id", gateway.Id));

        if (!string.IsNullOrEmpty(gateway.DefaultFlowId))
        {
            element.Add(new XAttribute("default", gateway.DefaultFlowId));
        }

        return element;
    }

    private XElement CreateSequenceFlowElement(BpmnSequenceFlow flow, XNamespace ns, SortedDictionary<string, string> namespaceDeclarations)
    {
        var element = new XElement(ns + "sequenceFlow", 
            new XAttribute("id", flow.Id),
            new XAttribute("sourceRef", flow.SourceRef),
            new XAttribute("targetRef", flow.TargetRef));

        // Add name from extension attributes if present
        var name = flow.Attributes?.TryGetValue("name", out var flowName) == true ? flowName : null;
        if (!string.IsNullOrEmpty(name))
        {
            element.Add(new XAttribute("name", name));
        }

        // Add condition expression if present
        if (!string.IsNullOrEmpty(flow.ConditionExpression))
        {
            var conditionElement = new XElement(ns + "conditionExpression", new XCData(flow.ConditionExpression));
            element.Add(conditionElement);
        }

        // Add priority with correct namespace if present
        if (flow.Priority.HasValue)
        {
            var priorityNs = GetPriorityNamespace(flow.Id, namespaceDeclarations);
            var priorityName = string.IsNullOrEmpty(priorityNs) 
                ? "priority" 
                : GetNamespacePrefix(priorityNs, namespaceDeclarations) + ":priority";
            
            var priorityAttr = string.IsNullOrEmpty(priorityNs) 
                ? new XAttribute("priority", flow.Priority.Value.ToString())
                : new XAttribute(XName.Get("priority", priorityNs), flow.Priority.Value.ToString());
                
            element.Add(priorityAttr);
        }

        return element;
    }

    private void AddEventDefinitions(XElement eventElement, BpmnEvent evt, XNamespace ns)
    {
        foreach (var definition in evt.Definitions)
        {
            switch (definition)
            {
                case TimerEventDefinition timer:
                    var timerElement = new XElement(ns + "timerEventDefinition");
                    if (!string.IsNullOrEmpty(timer.TimeDate))
                        timerElement.Add(new XElement(ns + "timeDate", timer.TimeDate));
                    if (!string.IsNullOrEmpty(timer.TimeDuration))
                        timerElement.Add(new XElement(ns + "timeDuration", timer.TimeDuration));
                    if (!string.IsNullOrEmpty(timer.TimeCycle))
                        timerElement.Add(new XElement(ns + "timeCycle", timer.TimeCycle));
                    eventElement.Add(timerElement);
                    break;

                case MessageEventDefinition message:
                    var messageElement = new XElement(ns + "messageEventDefinition");
                    if (!string.IsNullOrEmpty(message.MessageRef))
                        messageElement.Add(new XAttribute("messageRef", message.MessageRef));
                    if (!string.IsNullOrEmpty(message.CorrelationKey))
                        messageElement.Add(new XAttribute("correlationKey", message.CorrelationKey));
                    eventElement.Add(messageElement);
                    break;

                case SignalEventDefinition signal:
                    var signalElement = new XElement(ns + "signalEventDefinition");
                    if (!string.IsNullOrEmpty(signal.SignalRef))
                        signalElement.Add(new XAttribute("signalRef", signal.SignalRef));
                    eventElement.Add(signalElement);
                    break;

                case ErrorEventDefinition error:
                    var errorElement = new XElement(ns + "errorEventDefinition");
                    if (!string.IsNullOrEmpty(error.ErrorRef))
                        errorElement.Add(new XAttribute("errorRef", error.ErrorRef));
                    eventElement.Add(errorElement);
                    break;

                case ConditionalEventDefinition conditional:
                    var conditionalElement = new XElement(ns + "conditionalEventDefinition");
                    if (!string.IsNullOrEmpty(conditional.Condition))
                        conditionalElement.Add(new XElement(ns + "conditionExpression", new XCData(conditional.Condition)));
                    eventElement.Add(conditionalElement);
                    break;

                case TerminateEventDefinition:
                    eventElement.Add(new XElement(ns + "terminateEventDefinition"));
                    break;

                case CancelEventDefinition:
                    eventElement.Add(new XElement(ns + "cancelEventDefinition"));
                    break;

                case CompensationEventDefinition compensation:
                    var compensationElement = new XElement(ns + "compensateEventDefinition");
                    if (!string.IsNullOrEmpty(compensation.ActivityRef))
                        compensationElement.Add(new XAttribute("activityRef", compensation.ActivityRef));
                    eventElement.Add(compensationElement);
                    break;

                case EscalationEventDefinition escalation:
                    var escalationElement = new XElement(ns + "escalationEventDefinition");
                    if (!string.IsNullOrEmpty(escalation.EscalationRef))
                        escalationElement.Add(new XAttribute("escalationRef", escalation.EscalationRef));
                    eventElement.Add(escalationElement);
                    break;

                case LinkEventDefinition link:
                    var linkElement = new XElement(ns + "linkEventDefinition");
                    if (!string.IsNullOrEmpty(link.Name))
                        linkElement.Add(new XAttribute("name", link.Name));
                    eventElement.Add(linkElement);
                    break;
            }
        }
    }

    private void AddVendorExtensions(XElement element, IReadOnlyDictionary<string, string>? extensionAttributes, 
        SortedDictionary<string, string> namespaceDeclarations)
    {
        if (extensionAttributes == null || extensionAttributes.Count == 0) return;

        // Group extensions by namespace prefix for structured output
        var extensionsByPrefix = extensionAttributes
            .Where(e => e.Key.Contains(':'))
            .GroupBy(e => e.Key.Split(':')[0])
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var prefixGroup in extensionsByPrefix)
        {
            var prefix = prefixGroup.Key;
            if (!namespaceDeclarations.TryGetValue(prefix, out var namespaceUri)) continue;
            
            foreach (var ext in prefixGroup.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                var parts = ext.Key.Split(':', 2);
                if (parts.Length >= 2)
                {
                    var localName = parts[1];
                    
                    // Handle simple vendor attributes
                    if (localName == "assignee")
                    {
                        element.Add(new XAttribute(XNamespace.Get(namespaceUri) + "assignee", ext.Value));
                    }
                    // Add more vendor extension patterns as needed
                }
            }
        }
    }

    private void AddDataElements(XElement process, BpmnModel model, XNamespace ns)
    {
        // Add data objects, data stores, etc. if present
        foreach (var dataObject in model.DataObjects.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            var element = new XElement(ns + "dataObject", new XAttribute("id", dataObject.Id));
            if (!string.IsNullOrEmpty(dataObject.Name))
            {
                element.Add(new XAttribute("name", dataObject.Name));
            }
            process.Add(element);
        }

        foreach (var dataObjectRef in model.DataObjectReferences.OrderBy(d => d.Id, StringComparer.Ordinal))
        {
            process.Add(new XElement(ns + "dataObjectReference",
                new XAttribute("id", dataObjectRef.Id),
                new XAttribute("dataObjectRef", dataObjectRef.DataObjectRef)));
        }
    }

    private void AddGlobalElements(XElement definitions, BpmnModel model, XNamespace ns)
    {
        // Add global elements in canonical order
        foreach (var message in model.Messages.OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            var element = new XElement(ns + "message", new XAttribute("id", message.Id));
            if (!string.IsNullOrEmpty(message.Name))
            {
                element.Add(new XAttribute("name", message.Name));
            }
            definitions.Add(element);
        }

        foreach (var signal in model.Signals.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            var element = new XElement(ns + "signal", new XAttribute("id", signal.Id));
            if (!string.IsNullOrEmpty(signal.Name))
            {
                element.Add(new XAttribute("name", signal.Name));
            }
            definitions.Add(element);
        }

        foreach (var error in model.Errors.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            var element = new XElement(ns + "error", new XAttribute("id", error.Id));
            if (!string.IsNullOrEmpty(error.Name))
            {
                element.Add(new XAttribute("name", error.Name));
            }
            if (!string.IsNullOrEmpty(error.ErrorCode))
            {
                element.Add(new XAttribute("errorCode", error.ErrorCode));
            }
            definitions.Add(element);
        }

        foreach (var escalation in model.Escalations.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            var element = new XElement(ns + "escalation", new XAttribute("id", escalation.Id));
            if (!string.IsNullOrEmpty(escalation.Name))
            {
                element.Add(new XAttribute("name", escalation.Name));
            }
            if (!string.IsNullOrEmpty(escalation.EscalationCode))
            {
                element.Add(new XAttribute("escalationCode", escalation.EscalationCode));
            }
            definitions.Add(element);
        }
    }

    private static bool HasVendorExtensions(BpmnModel model)
    {
        return model.Tasks.Any(t => t.Attributes != null && t.Attributes.Count > 0) ||
               model.Events.Any(e => e.Attributes != null && e.Attributes.Count > 0) ||
               model.SequenceFlows.Any(f => f.Attributes != null && f.Attributes.Count > 0) ||
               model.RawMetadata?.PriorityAttributeNamespace?.Count > 0;
    }

    // Fixed: Get event name from Attributes
    private static string GetEventName(BpmnEvent evt)
    {
        // Check extension attributes for name
        return evt.Attributes?.TryGetValue("name", out var name) == true ? name : string.Empty;
    }

    // Fixed: Get attachedToRef from Attributes
    private static string GetAttachedToRef(BpmnEvent evt)
    {
        // Check extension attributes for attachedToRef
        if (evt.Attributes?.TryGetValue("attachedToRef", out var attachedRef) == true)
        {
            return attachedRef;
        }
        
        return string.Empty;
    }

    private string GetPriorityNamespace(string flowId, SortedDictionary<string, string> namespaceDeclarations)
    {
        // Check the model's raw metadata for priority namespace mappings
        // This would need access to the original model's raw metadata
        return string.Empty;
    }

    private static string GetNamespacePrefix(string namespaceUri, SortedDictionary<string, string> namespaceDeclarations)
    {
        return namespaceDeclarations.FirstOrDefault(kv => kv.Value == namespaceUri).Key ?? "ns";
    }

    private static string FormatXml(XElement element)
    {
        // Phase 8: Deterministic formatting
        return element.ToString(SaveOptions.DisableFormatting);
    }
}

