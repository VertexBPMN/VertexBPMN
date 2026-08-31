using System.Text.Json;
using System.Threading.Tasks;
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

    public NormalizedProjectionSerializer() : this(new BpmnParserOptions())
    {
    }
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

        // Add DI namespaces only if DI content will be emitted (shapes or edges)
        if (ShouldEmitDiagram(model))
        {
            namespaceDeclarations["bpmndi"] = "http://www.omg.org/spec/BPMN/20100524/DI";
            namespaceDeclarations["omgdc"] = "http://www.omg.org/spec/DD/20100524/DC";
            namespaceDeclarations["omgdi"] = "http://www.omg.org/spec/DD/20100524/DI";
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

        // NEW: Add Diagram Interchange (BPMNDiagram) if shapes/edges exist
        AddDiagramInterchange(definitions, model);

        return FormatXml(definitions);
    }

    private static bool ShouldEmitDiagram(BpmnModel model)
        => (model.Shapes is { Count: > 0 }) || (model.Edges is { Count: > 0 });

    private void CollectUsedNamespaces(BpmnModel model, SortedDictionary<string, string> namespaceDeclarations)
    {
        var knownNamespaces = new Dictionary<string, string>
        {
            ["camunda"] = "http://camunda.org/schema/1.0/bpmn",
            ["zeebe"] = "http://zeebe.io/schema/zeebe/1.0",
            ["flowable"] = "http://flowable.org/bpmn",
            ["activiti"] = "http://activiti.org/bpmn",
            ["cib"] = "http://cib.de/schema/bpmn",
            ["jbpm"] = "http://jbpm.org/bpmn",
            ["osmanthus"] = "http://osmanthus.io/bpmn",
            ["alfresco"] = "http://alfresco.org/bpmn",
            ["mcp"] = "http://vertexbpmn.io/mcp"
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

        // 1. Events
        var sortedEvents = model.Events
            .OrderBy(GetEventSortOrder)
            .ThenBy(e => e.Id, StringComparer.Ordinal);

        foreach (var evt in sortedEvents)
        {
            process.Add(CreateEventElement(evt, ns, namespaceDeclarations));
        }

        // 2. Activities
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

        // 3. Gateways
        var sortedGateways = model.Gateways
            .OrderBy(g => g.Type, StringComparer.Ordinal)
            .ThenBy(g => g.Id, StringComparer.Ordinal);

        foreach (var gateway in sortedGateways)
        {
            process.Add(CreateGatewayElement(gateway, ns));
        }

        // 4. Sequence flows
        var sortedFlows = model.SequenceFlows
            .OrderBy(f => f.Id, StringComparer.Ordinal);

        foreach (var flow in sortedFlows)
        {
            process.Add(CreateSequenceFlowElement(flow, ns, namespaceDeclarations));
        }

        // 5. Data elements
        AddDataElements(process, model, ns);
    }

    private void AddElementsInParseOrder(XElement process, BpmnModel model, XNamespace ns,
        SortedDictionary<string, string> namespaceDeclarations)
    {
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
        AddCamundaFormDataBlock(element, task.Attributes, namespaceDeclarations);
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
        AddCamundaFormDataBlock(element, subprocess.Attributes, namespaceDeclarations);
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
            if (!string.IsNullOrWhiteSpace(flow.ConditionExpressionLanguage))
                conditionElement.SetAttributeValue("language", flow.ConditionExpressionLanguage);
            element.Add(conditionElement);
        }

        // Add priority with correct namespace if present
        if (flow.Priority.HasValue)
        {
            var priorityNs = GetPriorityNamespace(flow.Id, namespaceDeclarations);
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
                        conditionalElement.Add(new XElement(ns + "condition", new XCData(conditional.Condition)));
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

    /// <summary>
    /// Adds vendor extension elements to the given task/event/subprocess element.
    /// </summary>
    public void AddVendorExtensions(XElement targetElement,
        IReadOnlyDictionary<string, string>? normalizedExtensions,
        SortedDictionary<string, string> namespaceDeclarations)
    {
        if (normalizedExtensions == null || normalizedExtensions.Count == 0) return;

        var extensionElements = GetOrCreateExtensionElements(targetElement);

        // Group extensions by vendor namespace for structured output
        var extensionsByVendor = normalizedExtensions
            .Where(kv => kv.Key.Contains(':'))
            .GroupBy(kv => kv.Key.Split(':')[0])
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var vendorGroup in extensionsByVendor)
        {
            var vendorPrefix = vendorGroup.Key;
            if (!namespaceDeclarations.TryGetValue(vendorPrefix, out var namespaceUri)) continue;

            var vendorNs = XNamespace.Get(namespaceUri);

            switch (vendorPrefix)
            {
                case "camunda":
                    AddCamundaExtensions(extensionElements, vendorGroup, vendorNs);
                    break;
                case "zeebe":
                    AddZeebeExtensions(extensionElements, vendorGroup, vendorNs);
                    break;
                case "flowable":
                    AddFlowableExtensions(extensionElements, vendorGroup, vendorNs);
                    break;
                case "activiti":
                    AddActivitiExtensions(extensionElements, vendorGroup, vendorNs);
                    break;
                case "cib":
                    AddCibExtensions(extensionElements, vendorGroup, vendorNs);
                    break;
                case "jbpm":
                    AddJbpmExtensions(extensionElements, vendorGroup, vendorNs);
                    break;
                case "osmanthus":
                    AddOsmanthusExtensions(extensionElements, vendorGroup, vendorNs);
                    break;
                case "alfresco":
                    AddAlfrescoExtensions(extensionElements, vendorGroup, vendorNs);
                    break;
                case "mcp":
                    AddMcpExtensions(extensionElements, vendorGroup, vendorNs);
                    break;
                default:
                    if (_options.NormalizeUnknownVendorExtensions)
                    {
                        AddGenericExtensions(extensionElements, vendorGroup, vendorNs, vendorPrefix);
                    }
                    break;
            }
        }
    }

    //private void AddVendorExtensions(XElement element, IReadOnlyDictionary<string, string>? extensionAttributes,
    //    SortedDictionary<string, string> namespaceDeclarations)
    //{
    //    if (extensionAttributes == null || extensionAttributes.Count == 0) return;

    //    // Group extensions by namespace prefix for structured output
    //    var extensionsByPrefix = extensionAttributes
    //        .Where(e => e.Key.Contains(':'))
    //        .GroupBy(e => e.Key.Split(':')[0])
    //        .OrderBy(g => g.Key, StringComparer.Ordinal);

    //    foreach (var prefixGroup in extensionsByPrefix)
    //    {
    //        var prefix = prefixGroup.Key;
    //        if (!namespaceDeclarations.TryGetValue(prefix, out var namespaceUri)) continue;

    //        foreach (var ext in prefixGroup.OrderBy(e => e.Key, StringComparer.Ordinal))
    //        {
    //            var parts = ext.Key.Split(':', 2);
    //            if (parts.Length >= 2)
    //            {
    //                var localName = parts[1];
    //                // Skip structured form field keys; handled separately
    //                if (prefix == "camunda" && localName.StartsWith("formField.", StringComparison.Ordinal))
    //                    continue;
    //                // Handle simple vendor attributes
    //                if (localName == "assignee")
    //                {
    //                    element.Add(new XAttribute(XNamespace.Get(namespaceUri) + "assignee", ext.Value));
    //                }
    //                // Extend with more vendor patterns as needed
    //            }
    //        }
    //    }
    //}

    private void AddCamundaFormDataBlock(XElement taskElement,
        IReadOnlyDictionary<string, string>? attributes,
        SortedDictionary<string, string> namespaceDeclarations)
    {
        if (attributes == null || attributes.Count == 0) return;
        if (!namespaceDeclarations.TryGetValue("camunda", out var camundaNsUri)) return;

        var camundaNs = XNamespace.Get(camundaNsUri);

        // Try flattened format first: camunda:formField.<fieldId>.<property> = value
        var formFieldEntries = attributes
            .Where(kv => kv.Key.StartsWith("camunda:formField.", StringComparison.Ordinal))
            .ToList();

        Dictionary<string, Dictionary<string, string>>? fieldData = null;

        if (formFieldEntries.Any())
        {
            // Process flattened format
            var grouped = formFieldEntries
                .Select(kv =>
                {
                    var prop = kv.Key.Substring("camunda:formField.".Length);
                    return (fieldId: "id", prop: prop, value: kv.Value);
                })
                .Where(t => !string.IsNullOrEmpty(t.fieldId) && !string.IsNullOrEmpty(t.prop))
                .GroupBy(t => t.fieldId!, StringComparer.Ordinal);

            fieldData = grouped.ToDictionary(
                g => g.Key,
                g => g.ToDictionary(entry => entry.prop, entry => entry.value)
            );
        }
        else if (attributes.TryGetValue("camunda:formFields", out var formFieldsJson))
        {
            // Try JSON format (legacy support)
            try
            {
                var formFields = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(formFieldsJson);
                if (formFields != null)
                {
                    fieldData = formFields.ToDictionary(
                        field => field.TryGetValue("id", out var id) ? id.ToString() ?? "" : "",
                        field => field.ToDictionary(
                            kv => kv.Key,
                            kv => kv.Value?.ToString() ?? ""
                        )
                    );
                }
            }
            catch (JsonException)
            {
                // JSON parsing failed, skip
                return;
            }
        }

        if (fieldData == null || !fieldData.Any())
            return;

        var formDataEl = new XElement(camundaNs + "formData");

        foreach (var (fieldId, fieldAttrs) in fieldData.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrEmpty(fieldId)) continue;

            var fieldEl = new XElement(camundaNs + "formField");
            fieldEl.Add(new XAttribute("id", fieldId));

            foreach (var (prop, value) in fieldAttrs)
            {
                if (prop == "id" || string.IsNullOrEmpty(value)) continue; // Skip id (already set) and empty values

                switch (prop.ToLowerInvariant())
                {
                    case "type":
                    case "name":
                    case "label":
                    case "defaultvalue":
                    case "datepattern":
                    case "required":
                    case "readable":
                    case "writable":
                    case "values":
                        fieldEl.SetAttributeValue(prop, value);
                        break;
                }
            }
            formDataEl.Add(fieldEl);
        }

        // Attach using extensionElements wrapper
        var existingExt = taskElement.Element(taskElement.GetDefaultNamespace() + "extensionElements");
        if (existingExt == null)
        {
            existingExt = new XElement(taskElement.GetDefaultNamespace() + "extensionElements");
            taskElement.Add(existingExt);
        }
        existingExt.Add(formDataEl);
    }
    private void AddDataElements(XElement process, BpmnModel model, XNamespace ns)
    {
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

    private void AddDiagramInterchange(XElement definitions, BpmnModel model)
    {
        if (!ShouldEmitDiagram(model)) return;

        // Namespaces already declared if we reached here
        var bpmndi = XNamespace.Get("http://www.omg.org/spec/BPMN/20100524/DI");
        var omgdc = XNamespace.Get("http://www.omg.org/spec/DD/20100524/DC");
        var omgdi = XNamespace.Get("http://www.omg.org/spec/DD/20100524/DI");

        // Deterministic IDs (simple pattern)
        var diagramId = $"BPMNDiagram_{model.ProcessId}";
        var planeId = $"BPMNPlane_{model.ProcessId}";

        var diagram = new XElement(bpmndi + "BPMNDiagram",
            new XAttribute("id", diagramId));

        var plane = new XElement(bpmndi + "BPMNPlane",
            new XAttribute("id", planeId),
            new XAttribute("bpmnElement", model.ProcessId));

        // Shapes (ordered deterministically by id)
        if (model.Shapes is { Count: > 0 })
        {
            foreach (var shape in model.Shapes.OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                var shapeEl = new XElement(bpmndi + "BPMNShape",
                    new XAttribute("id", shape.Id),
                    new XAttribute("bpmnElement", shape.BpmnElementId));

                // Bounds
                shapeEl.Add(new XElement(omgdc + "Bounds",
                    new XAttribute("x", shape.X),
                    new XAttribute("y", shape.Y),
                    new XAttribute("width", shape.Width),
                    new XAttribute("height", shape.Height)));

                plane.Add(shapeEl);
            }
        }

        // Edges (ordered deterministically by id)
        if (model.Edges is { Count: > 0 })
        {
            foreach (var edge in model.Edges.OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                var edgeEl = new XElement(bpmndi + "BPMNEdge",
                    new XAttribute("id", edge.Id),
                    new XAttribute("bpmnElement", edge.BpmnElementId));

                if (edge.Waypoints is { Count: > 0 })
                {
                    foreach (var wp in edge.Waypoints)
                    {
                        edgeEl.Add(new XElement(omgdi + "waypoint",
                            new XAttribute("x", wp.X),
                            new XAttribute("y", wp.Y)));
                    }
                }

                plane.Add(edgeEl);
            }
        }

        diagram.Add(plane);
        definitions.Add(diagram);
    }

    private static bool HasVendorExtensions(BpmnModel model)
    {
        //return model.Definitions.Any(t => t.Attributes != null && t.Attributes.Count > 0) ||
        return model.Tasks.Any(t => t.Attributes != null && t.Attributes.Count > 0) ||
               model.Events.Any(e => e.Attributes != null && e.Attributes.Count > 0) ||
               model.SequenceFlows.Any(f => f.Attributes != null && f.Attributes.Count > 0) ||
               model.RawMetadata?.PriorityAttributeNamespace?.Count > 0;
    }

    // Fixed: Get event name from Attributes
    private static string GetEventName(BpmnEvent evt)
    {
        return evt.Attributes?.TryGetValue("name", out var name) == true ? name : string.Empty;
    }

    // Fixed: Get attachedToRef from Attributes
    private static string GetAttachedToRef(BpmnEvent evt)
    {
        if (evt.Attributes?.TryGetValue("attachedToRef", out var attachedRef) == true)
        {
            return attachedRef;
        }
        return string.Empty;
    }

    private string GetPriorityNamespace(string flowId, SortedDictionary<string, string> namespaceDeclarations)
    {
        // Placeholder for future mapping from RawMetadata if needed
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


    private static XElement GetOrCreateExtensionElements(XElement targetElement)
    {
        var ns = targetElement.GetDefaultNamespace();
        var existing = targetElement.Element(ns + "extensionElements");
        if (existing != null) return existing;

        existing = new XElement(ns + "extensionElements");
        targetElement.Add(existing);
        return existing;
    }

    private void AddCamundaExtensions(XElement extensionElements, IGrouping<string, KeyValuePair<string, string>> vendorGroup, XNamespace camundaNs)
    {
        var extensions = vendorGroup.ToList();

        // Handle simple assignee
        var assignee = extensions.FirstOrDefault(kv => kv.Key.Contains("camunda:assignee"));
        if (!string.IsNullOrEmpty(assignee.Value))
        {
            extensionElements.Add(new XElement(camundaNs + "assignee", new XAttribute("value", assignee.Value)));
        }

        // Handle formData and formFields
        var formFieldEntries = extensions.Where(kv => kv.Key.StartsWith("camunda:formField.", StringComparison.Ordinal));
        if (formFieldEntries.Any())
        {
            var formDataEl = new XElement(camundaNs + "formData");

            var fieldGroups = formFieldEntries
                .Select(kv =>
                {
                    var parts = kv.Key.Split('.');
                    if (parts.Length >= 3) // camunda:formField.{fieldId}.{property}
                    {
                        return new { FieldId = parts[2], Property = parts[3], Value = kv.Value };
                    }
                    return null;
                })
                .Where(item => item is not null)
                .Select(item => item!)
                .GroupBy(item => item.FieldId)
                .OrderBy(g => g.Key);

            foreach (var fieldGroup in fieldGroups)
            {
                var fieldEl = new XElement(camundaNs + "formField", new XAttribute("id", fieldGroup.Key));

                foreach (var prop in fieldGroup.OrderBy(p => p.Property))
                {
                    switch (prop.Property)
                    {
                        case "type":
                        case "name":
                        case "label":
                        case "defaultValue":
                        case "datePattern":
                        case "required":
                        case "readable":
                        case "writable":
                            fieldEl.SetAttributeValue(prop.Property, prop.Value);
                            break;
                    }
                }
                formDataEl.Add(fieldEl);
            }

            if (formDataEl.HasElements)
            {
                extensionElements.Add(formDataEl);
            }
        }

        // Handle properties
        var propertyEntries = extensions.Where(kv => kv.Key.StartsWith("camunda:property.", StringComparison.Ordinal));
        if (propertyEntries.Any())
        {
            var propertiesEl = new XElement(camundaNs + "properties");

            foreach (var prop in propertyEntries.OrderBy(kv => kv.Key))
            {
                var propName = prop.Key.Substring("camunda:property.".Length);
                propertiesEl.Add(new XElement(camundaNs + "property",
                    new XAttribute("name", propName),
                    new XAttribute("value", prop.Value)));
            }

            extensionElements.Add(propertiesEl);
        }

        // Handle taskListeners
        AddIndexedElements(extensionElements, extensions, "camunda:taskListener", camundaNs, "taskListener",
            new[] { "event", "class", "expression" });
    }

    private void AddZeebeExtensions(XElement extensionElements, IGrouping<string, KeyValuePair<string, string>> vendorGroup, XNamespace zeebeNs)
    {
        var extensions = vendorGroup.ToList();

        // Handle taskDefinition
        var taskDefType = extensions.FirstOrDefault(kv => kv.Key == "zeebe:taskDefinition.type");
        if (!string.IsNullOrEmpty(taskDefType.Value))
        {
            extensionElements.Add(new XElement(zeebeNs + "taskDefinition", new XAttribute("type", taskDefType.Value)));
        }

        // Handle ioMapping
        var hasIoMappingEntries = extensions.Any(kv => kv.Key.StartsWith("zeebe:ioMapping", StringComparison.Ordinal));
        if (hasIoMappingEntries)
        {
            var ioMappingEntries = extensions.Where(kv => kv.Key.StartsWith("zeebe:", StringComparison.Ordinal));
            var ioMappingEl = new XElement(zeebeNs + "ioMapping");

            var inputs = ioMappingEntries.Where(kv => kv.Key.StartsWith("zeebe:input.", StringComparison.Ordinal));
            var outputs = ioMappingEntries.Where(kv => kv.Key.StartsWith("zeebe:output.", StringComparison.Ordinal));

            foreach (var input in inputs.OrderBy(kv => kv.Key))
            {
                var target = input.Key.Substring("zeebe:input.".Length);
                ioMappingEl.Add(new XElement(zeebeNs + "input",
                    new XAttribute("source", input.Value),
                    new XAttribute("target", target)));
            }

            foreach (var output in outputs.OrderBy(kv => kv.Key))
            {
                var target = output.Key.Substring("zeebe:output.".Length);
                ioMappingEl.Add(new XElement(zeebeNs + "output",
                    new XAttribute("source", output.Value),
                    new XAttribute("target", target)));
            }

            if (ioMappingEl.HasElements)
            {
                extensionElements.Add(ioMappingEl);
            }
        }

        // Handle taskHeaders
        var headerEntries = extensions.Where(kv => kv.Key.StartsWith("zeebe:taskHeaders.", StringComparison.Ordinal));
        if (headerEntries.Any())
        {
            var headersEl = new XElement(zeebeNs + "taskHeaders");

            foreach (var header in headerEntries.OrderBy(kv => kv.Key))
            {
                var key = header.Key.Substring("zeebe:taskHeaders.".Length);
                headersEl.Add(new XElement(zeebeNs + "header",
                    new XAttribute("key", key),
                    new XAttribute("value", header.Value)));
            }

            extensionElements.Add(headersEl);
        }
    }

    private void AddFlowableExtensions(XElement extensionElements, IGrouping<string, KeyValuePair<string, string>> vendorGroup, XNamespace flowableNs)
    {
        var extensions = vendorGroup.ToList();

        // Handle simple assignee
        var assignee = extensions.FirstOrDefault(kv => kv.Key == "flowable:assignee");
        if (!string.IsNullOrEmpty(assignee.Value))
        {
            extensionElements.Add(new XElement(flowableNs + "assignee", new XAttribute("value", assignee.Value)));
        }

        // Handle formFields similar to Camunda
        var formFieldEntries = extensions.Where(kv => kv.Key.StartsWith("flowable:formField.", StringComparison.Ordinal));
        if (formFieldEntries.Any())
        {
            AddFormFields(extensionElements, formFieldEntries, flowableNs, "flowable:formField.");
        }

        // Handle taskListeners
        AddIndexedElements(extensionElements, extensions, "flowable:taskListener", flowableNs, "taskListener",
            new[] { "event", "class", "expression" });
    }

    private void AddActivitiExtensions(XElement extensionElements, IGrouping<string, KeyValuePair<string, string>> vendorGroup, XNamespace activitiNs)
    {
        var extensions = vendorGroup.ToList();

        // Handle formProperties
        var formPropEntries = extensions.Where(kv => kv.Key.StartsWith("activiti:formProperty.", StringComparison.Ordinal));
        if (formPropEntries.Any())
        {
            var propGroups = formPropEntries
                .Select(kv =>
                {
                    var parts = kv.Key.Split('.');
                    if (parts.Length >= 3)
                    {
                        return new { PropertyId = parts[2], Attribute = parts[3], Value = kv.Value };
                    }
                    return null;
                })
                .Where(item => item is not null)
                .Select(item => item!)
                .GroupBy(item => item.PropertyId);

            foreach (var propGroup in propGroups.OrderBy(g => g.Key))
            {
                var propEl = new XElement(activitiNs + "formProperty", new XAttribute("id", propGroup.Key));

                foreach (var attr in propGroup.OrderBy(p => p.Attribute))
                {
                    propEl.SetAttributeValue(attr.Attribute, attr.Value);
                }

                extensionElements.Add(propEl);
            }
        }

        // Handle taskListeners and executionListeners
        AddIndexedElements(extensionElements, extensions, "activiti:taskListener", activitiNs, "taskListener",
            new[] { "event", "class", "expression", "delegateExpression" });
        AddIndexedElements(extensionElements, extensions, "activiti:executionListener", activitiNs, "executionListener",
            new[] { "event", "class", "expression", "delegateExpression" });

        // Handle candidateUsers and candidateGroups
        var candidateUsers = extensions.FirstOrDefault(kv => kv.Key == "activiti:candidateUsers");
        if (!string.IsNullOrEmpty(candidateUsers.Value))
        {
            extensionElements.Add(new XElement(activitiNs + "candidateUsers", new XAttribute("value", candidateUsers.Value)));
        }

        var candidateGroups = extensions.FirstOrDefault(kv => kv.Key == "activiti:candidateGroups");
        if (!string.IsNullOrEmpty(candidateGroups.Value))
        {
            extensionElements.Add(new XElement(activitiNs + "candidateGroups", new XAttribute("value", candidateGroups.Value)));
        }
    }

    private void AddCibExtensions(XElement extensionElements, IGrouping<string, KeyValuePair<string, string>> vendorGroup, XNamespace cibNs)
    {
        var extensions = vendorGroup.ToList();

        // Handle assignee
        var assignee = extensions.FirstOrDefault(kv => kv.Key == "cib:assignee");
        if (!string.IsNullOrEmpty(assignee.Value))
        {
            extensionElements.Add(new XElement(cibNs + "assignee", new XAttribute("value", assignee.Value)));
        }

        // Handle formFields
        var formFieldEntries = extensions.Where(kv => kv.Key.StartsWith("cib:formField.", StringComparison.Ordinal));
        if (formFieldEntries.Any())
        {
            AddFormFields(extensionElements, formFieldEntries, cibNs, "cib:formField.");
        }

        // Handle connectors
        var connectorEntries = extensions.Where(kv => kv.Key.StartsWith("cib:connector", StringComparison.Ordinal));
        if (connectorEntries.Any())
        {
            var connectorGroups = connectorEntries
                .Select(kv =>
                {
                    var parts = kv.Key.Split('.');
                    if (parts.Length >= 3)
                    {
                        var connectorId = parts[1].StartsWith("connector#") ? parts[1].Substring("connector#".Length) : parts[1];
                        return new { ConnectorId = connectorId, Property = parts[2], Value = kv.Value };
                    }
                    return null;
                })
                .Where(item => item is not null)
                .Select(item => item!)
                .GroupBy(item => item.ConnectorId);

            foreach (var connectorGroup in connectorGroups.OrderBy(g => g.Key))
            {
                var connectorEl = new XElement(cibNs + "connector");

                foreach (var prop in connectorGroup.OrderBy(p => p.Property))
                {
                    connectorEl.SetAttributeValue(prop.Property, prop.Value);
                }

                extensionElements.Add(connectorEl);
            }
        }

        // Handle aiModules
        var aiModuleEntries = extensions.Where(kv => kv.Key.StartsWith("cib:aiModule", StringComparison.Ordinal));
        if (aiModuleEntries.Any())
        {
            var moduleGroups = aiModuleEntries
                .Select(kv =>
                {
                    var parts = kv.Key.Split('.');
                    if (parts.Length >= 3)
                    {
                        var moduleId = parts[1].StartsWith("aiModule#") ? parts[1].Substring("aiModule#".Length) : parts[1];
                        return new { ModuleId = moduleId, Property = parts[2], Value = kv.Value };
                    }
                    return null;
                })
                .Where(item => item is not null)
                .Select(item => item!)
                .GroupBy(item => item.ModuleId);

            foreach (var moduleGroup in moduleGroups.OrderBy(g => g.Key))
            {
                var moduleEl = new XElement(cibNs + "aiModule");
                if (!moduleGroup.Key.StartsWith("#"))
                {
                    moduleEl.SetAttributeValue("type", moduleGroup.Key);
                }

                foreach (var prop in moduleGroup.Where(p => p.Property != "type").OrderBy(p => p.Property))
                {
                    moduleEl.SetAttributeValue(prop.Property, prop.Value);
                }

                extensionElements.Add(moduleEl);
            }
        }
    }

    private void AddJbpmExtensions(XElement extensionElements, IGrouping<string, KeyValuePair<string, string>> vendorGroup, XNamespace jbpmNs)
    {
        var extensions = vendorGroup.ToList();

        // Handle assignment
        var assignmentEntries = extensions.Where(kv => kv.Key.StartsWith("jbpm:assignment.", StringComparison.Ordinal));
        if (assignmentEntries.Any())
        {
            var assignmentEl = new XElement(jbpmNs + "assignment");

            foreach (var entry in assignmentEntries.OrderBy(kv => kv.Key))
            {
                var attr = entry.Key.Substring("jbpm:assignment.".Length);
                assignmentEl.SetAttributeValue(attr, entry.Value);
            }

            extensionElements.Add(assignmentEl);
        }

        // Handle workItemHandlers
        var handlerEntries = extensions.Where(kv => kv.Key.StartsWith("jbpm:workItemHandler", StringComparison.Ordinal));
        if (handlerEntries.Any())
        {
            var handlerGroups = handlerEntries
                .Select(kv =>
                {
                    var parts = kv.Key.Split('.');
                    if (parts.Length >= 3)
                    {
                        var handlerId = parts[1].StartsWith("workItemHandler#") ? parts[1].Substring("workItemHandler#".Length) : parts[1];
                        return new { HandlerId = handlerId, Property = parts[2], Value = kv.Value };
                    }
                    return null;
                })
                .Where(item => item is not null)
                .Select(item => item!)
                .GroupBy(item => item.HandlerId);

            foreach (var handlerGroup in handlerGroups.OrderBy(g => g.Key))
            {
                var handlerEl = new XElement(jbpmNs + "workItemHandler");
                if (!handlerGroup.Key.StartsWith("#"))
                {
                    handlerEl.SetAttributeValue("name", handlerGroup.Key);
                }

                foreach (var prop in handlerGroup.Where(p => p.Property != "name").OrderBy(p => p.Property))
                {
                    handlerEl.SetAttributeValue(prop.Property, prop.Value);
                }

                extensionElements.Add(handlerEl);
            }
        }
    }

    private void AddOsmanthusExtensions(XElement extensionElements, IGrouping<string, KeyValuePair<string, string>> vendorGroup, XNamespace osmanthusNs)
    {
        var extensions = vendorGroup.ToList();

        // Handle advance
        var advanceEntries = extensions.Where(kv => kv.Key.StartsWith("osmanthus:advance.", StringComparison.Ordinal));
        if (advanceEntries.Any())
        {
            var advanceEl = new XElement(osmanthusNs + "advance");
            foreach (var entry in advanceEntries.OrderBy(kv => kv.Key))
            {
                var attr = entry.Key.Substring("osmanthus:advance.".Length);
                advanceEl.SetAttributeValue(attr, entry.Value);
            }
            extensionElements.Add(advanceEl);
        }

        // Handle timeout
        var timeoutEntries = extensions.Where(kv => kv.Key.StartsWith("osmanthus:timeout.", StringComparison.Ordinal));
        if (timeoutEntries.Any())
        {
            var timeoutEl = new XElement(osmanthusNs + "timeout");
            foreach (var entry in timeoutEntries.OrderBy(kv => kv.Key))
            {
                var attr = entry.Key.Substring("osmanthus:timeout.".Length);
                timeoutEl.SetAttributeValue(attr, entry.Value);
            }
            extensionElements.Add(timeoutEl);
        }

        // Handle pdfTemplate
        var pdfEntries = extensions.Where(kv => kv.Key.StartsWith("osmanthus:pdfTemplate.", StringComparison.Ordinal));
        if (pdfEntries.Any())
        {
            var pdfEl = new XElement(osmanthusNs + "pdfTemplate");
            foreach (var entry in pdfEntries.OrderBy(kv => kv.Key))
            {
                var attr = entry.Key.Substring("osmanthus:pdfTemplate.".Length);
                pdfEl.SetAttributeValue(attr, entry.Value);
            }
            extensionElements.Add(pdfEl);
        }
    }

    private void AddAlfrescoExtensions(XElement extensionElements, IGrouping<string, KeyValuePair<string, string>> vendorGroup, XNamespace alfrescoNs)
    {
        var extensions = vendorGroup.ToList();

        // Handle formKey
        var formKey = extensions.FirstOrDefault(kv => kv.Key == "alfresco:formKey");
        if (!string.IsNullOrEmpty(formKey.Value))
        {
            extensionElements.Add(new XElement(alfrescoNs + "formKey", new XAttribute("value", formKey.Value)));
        }

        // Handle scriptTask
        var script = extensions.FirstOrDefault(kv => kv.Key == "alfresco:scriptTask.script");
        if (!string.IsNullOrEmpty(script.Value))
        {
            extensionElements.Add(new XElement(alfrescoNs + "scriptTask", new XAttribute("script", script.Value)));
        }
    }

    private void AddMcpExtensions(XElement extensionElements, IGrouping<string, KeyValuePair<string, string>> vendorGroup, XNamespace mcpNs)
    {
        var extensions = vendorGroup.ToList();

        var mcpServiceTaskEntries = extensions.Where(kv => kv.Key.StartsWith("mcp:mcpServiceTask.", StringComparison.Ordinal));
        if (mcpServiceTaskEntries.Any())
        {
            var mcpEl = new XElement(mcpNs + "mcpServiceTask");

            foreach (var entry in mcpServiceTaskEntries.OrderBy(kv => kv.Key))
            {
                var attr = entry.Key.Substring("mcp:mcpServiceTask.".Length);
                mcpEl.SetAttributeValue(attr, entry.Value);
            }

            extensionElements.Add(mcpEl);
        }
    }

    private void AddGenericExtensions(XElement extensionElements, IGrouping<string, KeyValuePair<string, string>> vendorGroup, XNamespace vendorNs, string prefix)
    {
        var extensions = vendorGroup.ToList();

        var elementGroups = extensions
            .Select(kv =>
            {
                var withoutPrefix = kv.Key.Substring($"{prefix}:".Length);
                var parts = withoutPrefix.Split('.');
                if (parts.Length >= 2)
                {
                    return new { ElementName = parts[0], Attribute = parts[1], Value = kv.Value };
                }
                return null;
            })
            .Where(item => item is not null)
            .Select(item => item!)
            .GroupBy(item => item.ElementName);

        foreach (var elementGroup in elementGroups.OrderBy(g => g.Key))
        {
            var element = new XElement(vendorNs + elementGroup.Key);

            foreach (var attr in elementGroup.OrderBy(a => a.Attribute))
            {
                if (attr.Attribute == "__text")
                {
                    element.Value = attr.Value;
                }
                else
                {
                    element.SetAttributeValue(attr.Attribute, attr.Value);
                }
            }

            extensionElements.Add(element);
        }
    }

    private void AddFormFields(XElement extensionElements, IEnumerable<KeyValuePair<string, string>> formFieldEntries, XNamespace ns, string prefix)
    {
        var fieldGroups = formFieldEntries
            .Select(kv =>
            {
                var parts = kv.Key.Split('.');
                if (parts.Length >= 3)
                {
                    return new { FieldId = parts[2], Property = parts[3], Value = kv.Value };
                }
                return null;
            })
            .Where(item => item is not null)
            .Select(item => item!)
            .GroupBy(item => item.FieldId)
            .OrderBy(g => g.Key);

        foreach (var fieldGroup in fieldGroups)
        {
            var fieldEl = new XElement(ns + "formField", new XAttribute("id", fieldGroup.Key));

            foreach (var prop in fieldGroup.OrderBy(p => p.Property))
            {
                fieldEl.SetAttributeValue(prop.Property, prop.Value);
            }

            extensionElements.Add(fieldEl);
        }
    }

    private void AddIndexedElements(XElement extensionElements, List<KeyValuePair<string, string>> extensions, string baseKey, XNamespace ns, string elementName, string[] supportedAttributes)
    {
        var indexedEntries = extensions.Where(kv => kv.Key.StartsWith($"{baseKey}#", StringComparison.Ordinal));
        if (!indexedEntries.Any()) return;

        var groups = indexedEntries
            .Select(kv =>
            {
                var parts = kv.Key.Split('.');
                if (parts.Length >= 2)
                {
                    return new { Index = parts[0], Attribute = parts[1], Value = kv.Value };
                }
                return null;
            })
            .Where(item => item is not null)
            .Select(item => item!)
            .GroupBy(item => item.Index);

        foreach (var group in groups.OrderBy(g => g.Key))
        {
            var element = new XElement(ns + elementName);

            foreach (var attr in group.Where(a => supportedAttributes.Contains(a.Attribute)).OrderBy(a => a.Attribute))
            {
                element.SetAttributeValue(attr.Attribute, attr.Value);
            }

            extensionElements.Add(element);
        }
    }
}



