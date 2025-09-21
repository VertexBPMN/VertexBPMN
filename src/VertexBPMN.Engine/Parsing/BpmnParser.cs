using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Entities.Modeling;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using Task = System.Threading.Tasks.Task;

namespace VertexBPMN.Engine.Parsing
{
    public class BpmnParser : IBpmnParser
    {
        private readonly ILogger<BpmnParser> _logger;
        private readonly Tracer _tracer;
        private readonly Dictionary<string, XDocument> _documentCache = new();

        public BpmnParser(ILogger<BpmnParser> logger, TracerProvider tracerProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tracer = tracerProvider.GetTracer("VertexBPMN");
        }

        public async Task<BpmnModel> ParseAsync(string bpmnXml, CancellationToken cancellationToken = default)
        {
            using var span = _tracer.StartActiveSpan("ParseBpmn");
            span.SetAttribute("xmlLength", bpmnXml?.Length ?? 0);

            if (string.IsNullOrEmpty(bpmnXml))
                throw new BpmnParseException("BPMN XML cannot be null or empty");

            try
            {
                XDocument doc;
                if (!_documentCache.TryGetValue(bpmnXml, out doc!))
                {
                    doc = await Task.Run(() => XDocument.Parse(bpmnXml), cancellationToken);
                    _documentCache[bpmnXml] = doc;
                }

                var ns = doc.Root?.Name.Namespace ?? throw new BpmnParseException("No namespace found in BPMN XML");
                var process = doc.Descendants(ns + "process").FirstOrDefault()
                    ?? throw new BpmnParseException("No <process> element found");
                var model = new BpmnModel
                {
                    Id = process.Attribute("id")?.Value ?? throw new BpmnParseException("Process missing id attribute"),
                    Name = (string?)process.Attribute("name") ?? "",
                    Events = ParseEvents(process, ns),
                    Tasks = ParseTasks(process, ns),
                    Gateways = ParseGateways(process, ns),
                    Subprocesses = ParseSubprocesses(process, ns),
                    SequenceFlows = ParseSequenceFlows(process, ns),
                    Lanes = ParseLanes(process, ns),
                    DataObjects = ParseDataObjects(process, ns),
                    Associations = ParseAssociations(process, ns),
                    TextAnnotations = ParseTextAnnotations(process, ns)
                };
                // Parse Kollaborationsdiagramme
                var collaboration = doc.Descendants(ns + "collaboration").FirstOrDefault();
                if (collaboration != null)
                {
                    model.Participants = ParseParticipants(collaboration, ns);
                    model.MessageFlows = ParseMessageFlows(collaboration, ns);
                }

                ValidateBpmnModel(model);
                _logger.LogInformation("Parsed BPMN process {ProcessId}", model.Id);
                span.SetStatus(Status.Ok);
                return model;
            }
            catch (XmlException ex)
            {
                span.SetStatus(Status.Error);//, ex.Message);
                _logger.LogError(ex, "Invalid BPMN XML format");
                throw new BpmnParseException("Invalid BPMN XML format", ex);
            }
            catch (Exception ex)
            {
                span.SetStatus(Status.Error.WithDescription(ex.Message));
                _logger.LogError(ex, "Error parsing BPMN XML");
                throw new BpmnParseException("Failed to parse BPMN XML", ex);
            }
        }

        private List<BpmnEvent> ParseEvents(XElement process, XNamespace ns)
        {
            var eventNames = new[] { "startEvent", "endEvent", "boundaryEvent", "intermediateCatchEvent", "intermediateThrowEvent" };
            var eventDefinitions = new[] { "timerEventDefinition", "messageEventDefinition", "errorEventDefinition",
                "signalEventDefinition", "compensateEventDefinition", "escalationEventDefinition",
                "conditionalEventDefinition", "linkEventDefinition", "cancelEventDefinition" };

            return process.Elements()
                .Where(e => eventNames.Contains(e.Name.LocalName))
                .Select(e =>
                {
                    var id = (string?)e.Attribute("id") ?? "";
                    var type = e.Name.LocalName;
                    var attachedTo = e.Name.LocalName == "boundaryEvent" ? (string?)e.Attribute("attachedToRef") : null;
                    var cancelActivity = e.Name.LocalName == "boundaryEvent" ? (string?)e.Attribute("cancelActivity") != "false" : true;
                    var isCompensation = e.Elements(ns + "compensateEventDefinition").Any();
                    var eventDefinitionType = eventDefinitions.FirstOrDefault(def => e.Element(ns + def) != null)?.Replace("EventDefinition", "").ToLower();

                    return new BpmnEvent(id, type, attachedTo, isCompensation, cancelActivity, eventDefinitionType);
                }).ToList();
        }

        private List<BpmnTask> ParseTasks(XElement process, XNamespace ns)
        {
            return process.Elements()
                .Where(e => e.Name.LocalName.EndsWith("Task") || e.Name.LocalName == "callActivity")
                .Select(e =>
                {
                    var id = (string?)e.Attribute("id") ?? "";
                    var type = e.Name.LocalName;
                    var implementation = (string?)e.Attribute("implementation");
                    var attributes = new Dictionary<string, string>();

                    // ScriptTask-spezifisch
                    if (type == "scriptTask")
                    {
                        var scriptFormat = (string?)e.Attribute("scriptFormat");
                        var scriptNode = e.Element(ns + "script")?.Value;
                        var resultVar = (string?)e.Attribute("resultVariable");
                        if (!string.IsNullOrEmpty(scriptFormat)) attributes["scriptFormat"] = scriptFormat;
                        if (!string.IsNullOrEmpty(scriptNode)) attributes["script"] = scriptNode;
                        if (!string.IsNullOrEmpty(resultVar)) attributes["resultVariable"] = resultVar;
                    }

                    // UserTask-spezifisch
                    if (type == "userTask")
                    {
                        var potentialOwner = e.Elements(ns + "potentialOwner").FirstOrDefault();
                        if (potentialOwner != null)
                        {
                            var expression = potentialOwner.Element(ns + "resourceAssignmentExpression")?
                                .Element(ns + "formalExpression")?.Value;
                            if (!string.IsNullOrEmpty(expression))
                                attributes["potentialOwner"] = expression;
                        }
                    }

                    // Extension Elements
                    var ext = e.Element(ns + "extensionElements");
                    if (ext != null)
                    {
                        // Generische Eigenschaften
                        foreach (var prop in ext.Elements(ns + "property"))
                        {
                            var name = (string?)prop.Attribute("name");
                            var value = (string?)prop.Attribute("value");
                            if (!string.IsNullOrEmpty(name) && value != null)
                                attributes[name] = value;
                        }
                        // NEU: Camunda properties Wrapper + generische properties in beliebigen Namespaces
                        ExtractPropertiesFromAnyNamespace(ext, attributes);

                        // Tool-spezifische Erweiterungen
                        ParseToolExtensions(ext, attributes);
                    }

                    return new BpmnTask(id, type, implementation, attributes);
                }).ToList();
        }

        private List<BpmnGateway> ParseGateways(XElement process, XNamespace ns)
        {
            return process.Elements()
                .Where(e => e.Name.LocalName.EndsWith("Gateway"))
                .Select(e => new BpmnGateway((string?)e.Attribute("id") ?? "", e.Name.LocalName))
                .ToList();
        }

        private List<BpmnSubprocess> ParseSubprocesses(XElement process, XNamespace ns)
        {
            return process.Elements()
                .Where(e => e.Name.LocalName == "subProcess" || e.Name.LocalName == "adHocSubProcess")
                .Select(e =>
                {
                    var id = (string?)e.Attribute("id") ?? "";
                    var mi = e.Elements(ns + "multiInstanceLoopCharacteristics").FirstOrDefault();
                    var isMultiInstance = mi != null;
                    var isSequential = mi?.Attribute("isSequential")?.Value == "true";
                    var loopCardinality = mi?.Element(ns + "loopCardinality")?.Value;
                    var isEventSubprocess = (string?)e.Attribute("triggeredByEvent") == "true";
                    var isTransaction = (string?)e.Attribute("transaction") == "true";

                    int? parsedCardinality = int.TryParse(loopCardinality, out var card) ? card : null;
                    return new BpmnSubprocess(id, isMultiInstance, isEventSubprocess, isTransaction, isSequential, parsedCardinality);
                }).ToList();
        }

        private List<BpmnSequenceFlow> ParseSequenceFlows(XElement process, XNamespace ns)
        {
            return process.Elements(ns + "sequenceFlow")
                .Select(e =>
                {
                    var flow = new BpmnSequenceFlow(
                        (string?)e.Attribute("id") ?? "",
                        (string?)e.Attribute("sourceRef") ?? "",
                        (string?)e.Attribute("targetRef") ?? "");

                    var condition = e.Element(ns + "conditionExpression")?.Value;
                    if (!string.IsNullOrWhiteSpace(condition) && flow.Attributes is IDictionary<string, object> dict)
                    {
                        dict["conditionExpression"] = condition.Trim();
                    }
                    return flow;
                })
                .ToList();
        }

        private List<BpmnLane> ParseLanes(XElement process, XNamespace ns)
        {
            return process.Elements(ns + "lane")
                .Select(l =>
                {
                    var id = (string?)l.Attribute("id") ?? "";
                    var name = (string?)l.Attribute("name") ?? "";
                    var flowNodeRefs = l.Elements(ns + "flowNodeRef").Select(fn => fn.Value).ToList();
            return new BpmnLane(id, name, flowNodeRefs);
        }).ToList();
        }

        private List<BpmnDataObject> ParseDataObjects(XElement process, XNamespace ns)
        {
            return process.Elements(ns + "dataObject")
                .Select(d =>
                {
                    var id = (string?)d.Attribute("id") ?? "";
                    var name = (string?)d.Attribute("name") ?? "";
                    return new BpmnDataObject(id, name);
                }).ToList();
        }

        private List<BpmnAssociation> ParseAssociations(XElement process, XNamespace ns)
        {
            return process.Elements(ns + "association")
                .Select(a => 
                {
                    var id = (string?)a.Attribute("id") ?? "";
                    var sourceRef = (string?)a.Attribute("sourceRef") ?? "";
                    var targetRef = (string?)a.Attribute("targetRef") ?? "";
                    return new BpmnAssociation ( id,sourceRef,targetRef);
                }).ToList();
        }

        private List<BpmnTextAnnotation> ParseTextAnnotations(XElement process, XNamespace ns)
        {
            return process.Elements(ns + "textAnnotation")
                .Select(t =>
                {
                    var id = (string?)t.Attribute("id") ?? "";
                    var text = t.Element(ns + "text")?.Value ?? "";
                    return new BpmnTextAnnotation(id, text);
                }).ToList();
        }

        private List<BpmnParticipant> ParseParticipants(XElement collaboration, XNamespace ns)
        {
            return collaboration.Elements(ns + "participant")
                .Select(p =>
                {
                    var id = (string?)p.Attribute("id") ?? "";
                    var processRef = (string?)p.Attribute("processRef") ?? "";
                    return new BpmnParticipant(  id, processRef );
                }).ToList();
        }

        private List<BpmnMessageFlow> ParseMessageFlows(XElement collaboration, XNamespace ns)
        {
            return collaboration.Elements(ns + "messageFlow")
                .Select(mf => new BpmnMessageFlow
                (
                    Id : (string?)mf.Attribute("id") ?? "",
                    SourceRef : (string?)mf.Attribute("sourceRef") ?? "",
                    TargetRef: (string?)mf.Attribute("targetRef") ?? ""
                )).ToList();
        }

        /// <summary>
        /// Extrahiert <properties>/<property>-Strukturen aus beliebigen Namespaces (z.B. camunda:properties).
        /// </summary>
        private static void ExtractPropertiesFromAnyNamespace(XElement extensionElements, IDictionary<string, string> attributes)
        {
            // Alle Elemente deren lokaler Name 'properties' ist
            var propertyContainers = extensionElements
                .Elements()
                .Where(x => x.Name.LocalName == "properties");

            foreach (var container in propertyContainers)
            {
                foreach (var prop in container.Elements().Where(p => p.Name.LocalName == "property"))
                {
                    var name = (string?)prop.Attribute("name");
                    var value = (string?)prop.Attribute("value");
                    if (!string.IsNullOrEmpty(name) && value != null)
                        attributes[name] = value;
                }
            }
        }

        /// <summary>
        /// Parst Tool-spezifische Erweiterungen aus <extensionElements> und speichert sie in den Attributen.
        /// Unterstützt Camunda, Zeebe, Activiti, Flowable, CIB, jBPM, Osmanthus, Alfresco und generische Namespaces.
        /// </summary>
        private void ParseToolExtensions(XElement ext, Dictionary<string, string> attributes)
        {
            // Camunda
            var camundaNs = "http://camunda.org/schema/1.0/bpmn";
            var camundaPropsWrapper = ext.Element(XName.Get("properties", camundaNs));
            if (camundaPropsWrapper != null)
            {
                foreach (var p in camundaPropsWrapper.Elements(XName.Get("property", camundaNs)))
                {
                    var name = (string?)p.Attribute("name");
                    var value = (string?)p.Attribute("value");
                    if (!string.IsNullOrEmpty(name) && value != null)
                        attributes[name] = value;
                }
            }


            var camundaAssignee = ext.Element(XName.Get("assignee", camundaNs))?.Attribute("value")?.Value;
            if (!string.IsNullOrEmpty(camundaAssignee)) attributes["camunda:assignee"] = camundaAssignee;

            var camundaFormFields = ext.Elements(XName.Get("formField", camundaNs))
                .Select(ff => new
                {
                    Id = (string?)ff.Attribute("id") ?? "",
                    Name = (string?)ff.Attribute("name") ?? "",
                    Type = (string?)ff.Attribute("type") ?? ""
                }).ToList();
            if (camundaFormFields.Any()) attributes["camunda:formFields"] = JsonSerializer.Serialize(camundaFormFields);

            // Zeebe
            var zeebeNs = "http://zeebe.io/schema/zeebe/1.0";
            var taskDefinition = ext.Element(XName.Get("taskDefinition", zeebeNs))?.Attribute("type")?.Value;
            if (!string.IsNullOrEmpty(taskDefinition)) attributes["zeebe:taskDefinition"] = taskDefinition;

            var ioMapping = ext.Element(XName.Get("ioMapping", zeebeNs))?.Elements()
                .ToDictionary(io => io.Attribute("target")?.Value ?? "", io => io.Attribute("source")?.Value ?? "");
            if (ioMapping != null && ioMapping.Any()) attributes["zeebe:ioMapping"] = JsonSerializer.Serialize(ioMapping);

            // Activiti
            var activitiNs = "http://activiti.org/bpmn";
            var activitiFormProperty = ext.Element(XName.Get("formProperty", activitiNs));
            if (activitiFormProperty != null)
            {
                var props = activitiFormProperty.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value);
                attributes["activiti:formProperty"] = JsonSerializer.Serialize(props);
            }

            // Flowable
            var flowableNs = "http://flowable.org/bpmn";
            var flowableAssignee = ext.Element(XName.Get("assignee", flowableNs))?.Attribute("value")?.Value;
            if (!string.IsNullOrEmpty(flowableAssignee)) attributes["flowable:assignee"] = flowableAssignee;

            var flowableTaskListeners = ext.Elements(XName.Get("taskListener", flowableNs))
                .Select(tl => new
                {
                    Event = (string?)tl.Attribute("event") ?? "",
                    Class = (string?)tl.Attribute("class") ?? "",
                    Expression = (string?)tl.Attribute("expression") ?? ""
                }).ToList();
            if (flowableTaskListeners.Any()) attributes["flowable:taskListeners"] = JsonSerializer.Serialize(flowableTaskListeners);

            var flowableFormFields = ext.Elements(XName.Get("formField", flowableNs))
                .Select(ff => new
                {
                    Id = (string?)ff.Attribute("id") ?? "",
                    Name = (string?)ff.Attribute("name") ?? "",
                    Type = (string?)ff.Attribute("type") ?? ""
                }).ToList();
            if (flowableFormFields.Any()) attributes["flowable:formFields"] = JsonSerializer.Serialize(flowableFormFields);

            // CIB seven/flow
            var cibNs = "http://cib.de/schema/bpmn";
            var cibAssignee = ext.Element(XName.Get("assignee", cibNs))?.Attribute("value")?.Value;
            if (!string.IsNullOrEmpty(cibAssignee)) attributes["cib:assignee"] = cibAssignee;

            var cibFormFields = ext.Elements(XName.Get("formField", cibNs))
                .Select(ff => new
                {
                    Id = (string?)ff.Attribute("id") ?? "",
                    Name = (string?)ff.Attribute("name") ?? "",
                    Type = (string?)ff.Attribute("type") ?? ""
                }).ToList();
            if (cibFormFields.Any()) attributes["cib:formFields"] = JsonSerializer.Serialize(cibFormFields);

            var connectors = ext.Elements(XName.Get("connector", cibNs))
                .Select(c => new
                {
                    Id = (string?)c.Attribute("id") ?? "",
                    Type = (string?)c.Attribute("type") ?? "",
                    Url = (string?)c.Attribute("url") ?? ""
                }).ToList();
            if (connectors.Any()) attributes["cib:connectors"] = JsonSerializer.Serialize(connectors);

            var aiModules = ext.Elements(XName.Get("aiModule", cibNs))
                .Select(ai => new
                {
                    Type = (string?)ai.Attribute("type") ?? "",
                    Model = (string?)ai.Attribute("model") ?? ""
                }).ToList();
            if (aiModules.Any()) attributes["cib:aiModules"] = JsonSerializer.Serialize(aiModules);

            // jBPM
            var jbpmNs = "http://jbpm.org/bpmn";
            var jbpmAssignment = ext.Element(XName.Get("assignment", jbpmNs));
            if (jbpmAssignment != null)
            {
                var actorId = (string?)jbpmAssignment.Attribute("actorId");
                var groupId = (string?)jbpmAssignment.Attribute("groupId");
                if (!string.IsNullOrEmpty(actorId)) attributes["jbpm:actorId"] = actorId;
                if (!string.IsNullOrEmpty(groupId)) attributes["jbpm:groupId"] = groupId;
            }

            var workItemHandlers = ext.Elements(XName.Get("workItemHandler", jbpmNs))
                .Select(w => new
                {
                    Name = (string?)w.Attribute("name") ?? "",
                    Class = (string?)w.Attribute("class") ?? ""
                }).ToList();
            if (workItemHandlers.Any()) attributes["jbpm:workItemHandlers"] = JsonSerializer.Serialize(workItemHandlers);

            // Osmanthus
            var osmanthusNs = "http://osmanthus.io/bpmn";
            var advance = ext.Element(XName.Get("advance", osmanthusNs));
            if (advance != null)
            {
                var advanceType = (string?)advance.Attribute("type");
                var target = (string?)advance.Attribute("target");
                if (!string.IsNullOrEmpty(advanceType)) attributes["osmanthus:advanceType"] = advanceType;
                if (!string.IsNullOrEmpty(target)) attributes["osmanthus:advanceTarget"] = target;
            }

            var timeout = ext.Element(XName.Get("timeout", osmanthusNs));
            if (timeout != null)
            {
                var duration = (string?)timeout.Attribute("duration");
                var action = (string?)timeout.Attribute("action");
                if (!string.IsNullOrEmpty(duration)) attributes["osmanthus:timeoutDuration"] = duration;
                if (!string.IsNullOrEmpty(action)) attributes["osmanthus:timeoutAction"] = action;
            }

            var pdfTemplate = ext.Element(XName.Get("pdfTemplate", osmanthusNs));
            if (pdfTemplate != null)
            {
                var templateId = (string?)pdfTemplate.Attribute("templateId");
                var output = (string?)pdfTemplate.Attribute("output");
                if (!string.IsNullOrEmpty(templateId)) attributes["osmanthus:pdfTemplateId"] = templateId;
                if (!string.IsNullOrEmpty(output)) attributes["osmanthus:pdfOutput"] = output;
            }

            // Alfresco
            var alfrescoNs = "http://alfresco.org/bpmn";
            var formKey = ext.Element(XName.Get("formKey", alfrescoNs))?.Attribute("value")?.Value;
            if (!string.IsNullOrEmpty(formKey)) attributes["alfresco:formKey"] = formKey;

            var scriptTask = ext.Element(XName.Get("scriptTask", alfrescoNs));
            if (scriptTask != null)
            {
                var script = (string?)scriptTask.Attribute("script");
                if (!string.IsNullOrEmpty(script)) attributes["alfresco:script"] = script;
            }

            // MCP ServiceTask
            var mcpNs = "http://camunda.org/schema/1.0/bpmn";
            var mcpNsVertex = "http://vertexbpmn.io/mcp";
            var mcpServiceTask = ext.Element(XName.Get("mcpServiceTask", mcpNs));
            mcpServiceTask = mcpServiceTask ?? ext.Element(XName.Get("mcpServiceTask", mcpNsVertex)); // fallback ohne Namespace
            if (mcpServiceTask != null)
            {
                if (mcpServiceTask.Attribute("mcpServerUrl") is { } serverUrl) attributes["mcpServerUrl"] = serverUrl.Value;
                if (mcpServiceTask.Attribute("mcpMethod") is { } method) attributes["mcpMethod"] = method.Value;
                if (mcpServiceTask.Attribute("mcpParams") is { } paramsAttr) attributes["mcpParams"] = paramsAttr.Value;
            }
            // Generische Erweiterungen für unbekannte Namespaces
            foreach (var element in ext.Elements())
            {
                var ns = element.Name.NamespaceName;
                if (!new[] { camundaNs, zeebeNs, activitiNs, flowableNs, cibNs, jbpmNs, osmanthusNs, alfrescoNs }.Contains(ns))
                {
                    var key = $"{ns}:{element.Name.LocalName}";
                    var value = element.Value ?? JsonSerializer.Serialize(element.Attributes().ToDictionary(a => a.Name.LocalName, a => a.Value));
                    attributes[key] = value;
                }
            }
        }
        
        private void ValidateBpmnModel(BpmnModel model)
        {
            if (string.IsNullOrEmpty(model.Id))
                throw new BpmnParseException("Process ID is missing");

            var flowNodeIds = model.Events.Select(e => e.Id)
                .Concat(model.Tasks.Select(t => t.Id))
                .Concat(model.Gateways.Select(g => g.Id))
                .Concat(model.Subprocesses.Select(s => s.Id))
                .Concat(model.SequenceFlows.Select(s => s.Id))
                .ToHashSet();

            foreach (var flow in model.SequenceFlows)
            {
                if (!flowNodeIds.Contains(flow.Id))
                    throw new BpmnParseException($"SequenceFlow {flow.Id} references invalid sourceRef {flow.SourceRef}");
            }

            foreach (var evt in model.Events.Where(e => e.Type == "boundaryEvent" && !string.IsNullOrEmpty(e.AttachedToRef)))
            {
                if (!model.Tasks.Any(t => t.Id == evt.AttachedToRef) && !model.Subprocesses.Any(s => s.Id == evt.AttachedToRef))
                    throw new BpmnParseException($"BoundaryEvent {evt.Id} references invalid attachedToRef {evt.AttachedToRef}");
            }

            foreach (var task in model.Tasks)
            {
                // mcpServiceTask base validation
                if (task.Type == "mcpServiceTask" && (!task.Attributes.ContainsKey("mcpServerUrl") || !task.Attributes.ContainsKey("mcpMethod")))
                    throw new BpmnParseException($"mcpServiceTask {task.Id} requires mcpServerUrl and mcpMethod attributes");

                // Zeebe ioMapping
                if (task.Attributes.TryGetValue("zeebe:ioMapping", out var zeebeIo))
                {
                    TryDeserialize<Dictionary<string, string>>(zeebeIo, $"Invalid zeebe:ioMapping format in task {task.Id}");
                }

                // Flowable taskListeners
                if (task.Attributes.TryGetValue("flowable:taskListeners", out var flowableListeners))
                {
                    TryDeserialize<List<Dictionary<string, object>>>(flowableListeners, $"Invalid flowable:taskListeners format in task {task.Id}");
                }

                // Camunda
                if (task.Attributes.TryGetValue("camunda:formFields", out var camundaFormFields))
                    TryDeserialize<List<Dictionary<string, object>>>(camundaFormFields, $"Invalid camunda:formFields format in task {task.Id}");
                // camunda:assignee is a simple string -> no structure check

                // Activiti
                if (task.Attributes.TryGetValue("activiti:formProperty", out var activitiFormProp))
                    TryDeserialize<Dictionary<string, string>>(activitiFormProp, $"Invalid activiti:formProperty format in task {task.Id}");

                // Flowable
                if (task.Attributes.TryGetValue("flowable:formFields", out var flowableFormFields))
                    TryDeserialize<List<Dictionary<string, object>>>(flowableFormFields, $"Invalid flowable:formFields format in task {task.Id}");
                // flowable:assignee simple string

                // CIB
                if (task.Attributes.TryGetValue("cib:formFields", out var cibFormFields))
                    TryDeserialize<List<Dictionary<string, object>>>(cibFormFields, $"Invalid cib:formFields format in task {task.Id}");
                if (task.Attributes.TryGetValue("cib:connectors", out var cibConnectors))
                {
                    var connectors = TryDeserialize<List<Dictionary<string, object>>>(cibConnectors, $"Invalid cib:connectors format in task {task.Id}");
                    // ensure required keys
                    if (connectors.Any(c => !c.ContainsKey("Id") || !c.ContainsKey("Type")))
                        throw new BpmnParseException($"cib:connectors entries require Id and Type in task {task.Id}");
                }
                if (task.Attributes.TryGetValue("cib:aiModules", out var cibAiModules))
                    TryDeserialize<List<Dictionary<string, object>>>(cibAiModules, $"Invalid cib:aiModules format in task {task.Id}");

                // jBPM
                if (task.Attributes.TryGetValue("jbpm:workItemHandlers", out var jbpmHandlers))
                    TryDeserialize<List<Dictionary<string, object>>>(jbpmHandlers, $"Invalid jbpm:workItemHandlers format in task {task.Id}");
                // jbpm:actorId / jbpm:groupId simple strings

                // Osmanthus
                // Attributes are flattened (osmanthus:advanceType, osmanthus:advanceTarget, osmanthus:timeoutDuration, osmanthus:timeoutAction, osmanthus:pdfTemplateId, osmanthus:pdfOutput)
                if (task.Attributes.ContainsKey("osmanthus:advanceType") ^ task.Attributes.ContainsKey("osmanthus:advanceTarget"))
                    throw new BpmnParseException($"Osmanthus advance requires both advanceType and advanceTarget when one is present (task {task.Id})");
                if (task.Attributes.ContainsKey("osmanthus:timeoutDuration") ^ task.Attributes.ContainsKey("osmanthus:timeoutAction"))
                    throw new BpmnParseException($"Osmanthus timeout requires both timeoutDuration and timeoutAction when one is present (task {task.Id})");
                if (task.Attributes.ContainsKey("osmanthus:pdfTemplateId") && !task.Attributes.ContainsKey("osmanthus:pdfOutput"))
                {
                    // output optional, so only validate templateId not empty
                    if (string.IsNullOrWhiteSpace(task.Attributes["osmanthus:pdfTemplateId"]))
                        throw new BpmnParseException($"Osmanthus pdfTemplateId cannot be empty (task {task.Id})");
                }

                // Alfresco
                // alfresco:formKey simple string
                // alfresco:script simple string

                // MCP Service Task extra optional validation
                if (task.Type == "mcpServiceTask" && task.Attributes.TryGetValue("mcpParams", out var mcpParams))
                {
                    // mcpParams expected JSON object (dictionary) or array – try parse as object first then array
                    if (!TryDeserializeSilently<Dictionary<string, object>>(mcpParams) &&
                        !TryDeserializeSilently<List<object>>(mcpParams))
                        throw new BpmnParseException($"Invalid mcpParams JSON in mcpServiceTask {task.Id}");
                }

                // Generic JSON-looking attributes (heuristic) - skip known validated keys
                foreach (var kvp in task.Attributes.Where(a =>
                             a.Value is { } v &&
                             v.Length > 1 &&
                             (v.TrimStart().StartsWith("{") || v.TrimStart().StartsWith("[")) &&
                             IsPotentialJsonExtensionKey(a.Key)))
                {
                    // best-effort generic validation
                    TryDeserialize<object>(kvp.Value, $"Invalid JSON structure for extension attribute {kvp.Key} on task {task.Id}");
                }
            }

            static bool IsPotentialJsonExtensionKey(string key)
            {
                // exclude simple known textual keys
                return !(key.EndsWith("assignee", StringComparison.OrdinalIgnoreCase) ||
                         key.EndsWith("formKey", StringComparison.OrdinalIgnoreCase) ||
                         key.EndsWith("script", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("actorId", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("groupId", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("advanceType", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("advanceTarget", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("timeoutDuration", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("timeoutAction", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("pdfTemplateId", StringComparison.OrdinalIgnoreCase) ||
                         key.Contains("pdfOutput", StringComparison.OrdinalIgnoreCase) ||
                         key.StartsWith("mcpServerUrl", StringComparison.OrdinalIgnoreCase) ||
                         key.StartsWith("mcpMethod", StringComparison.OrdinalIgnoreCase));
            }

            static T TryDeserialize<T>(string json, string errorMessage)
            {
                try
                {
                    var result = JsonSerializer.Deserialize<T>(json);
                    if (result == null)
                        throw new BpmnParseException(errorMessage);
                    return result;
                }
                catch (JsonException)
                {
                    throw new BpmnParseException(errorMessage);
                }
            }

            static bool TryDeserializeSilently<T>(string json)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(json) != null;
                }
                catch
                {
                    return false;
                }
            }
        }
        /// <summary>
        /// Serialisiert ein <see cref="BpmnModel"/> zurück in BPMN 2.0-XML.
        /// </summary>
        /// <param name="model">Das zu serialisierende <see cref="BpmnModel"/>.</param>
        /// <returns>Der BPMN-XML-String.</returns>
        public string Serialize(BpmnModel model)
        {
            var ns = "http://www.omg.org/spec/BPMN/20100524/MODEL";
            var doc = new XDocument(
                new XElement(XName.Get("definitions", ns),
                    new XElement(XName.Get("process", ns),
                        new XAttribute("id", model.Id),
                        new XAttribute("name", model.Name),
                        model.Events.Select(e => new XElement(XName.Get(e.Type, ns),
                            new XAttribute("id", e.Id),
                            e.AttachedToRef != null ? new XAttribute("attachedToRef", e.AttachedToRef) : null,
                            e.CancelActivity == false ? new XAttribute("cancelActivity", "false") : null,
                            e.EventDefinitionType != null ? new XElement(XName.Get($"{e.EventDefinitionType}EventDefinition", ns)) : null)),
                        model.Tasks.Select(t =>
                        {
                            var element = new XElement(XName.Get(t.Type, ns),
                                new XAttribute("id", t.Id),
                                t.Implementation != null ? new XAttribute("implementation", t.Implementation) : null);

                            if (t.Type == "scriptTask")
                            {
                                if (t.Attributes.TryGetValue("scriptFormat", out var scriptFormat))
                                    element.Add(new XAttribute("scriptFormat", scriptFormat));
                                if (t.Attributes.TryGetValue("resultVariable", out var resultVar))
                                    element.Add(new XAttribute("resultVariable", resultVar));
                                if (t.Attributes.TryGetValue("script", out var scriptContent))
                                    element.Add(new XElement(XName.Get("script", ns), scriptContent));
                            }

                            if (t.Type == "userTask" && t.Attributes.TryGetValue("potentialOwner", out var owner))
                            {
                                element.Add(new XElement(XName.Get("potentialOwner", ns),
                                    new XElement(XName.Get("resourceAssignmentExpression", ns),
                                        new XElement(XName.Get("formalExpression", ns), owner))));
                            }
                           
                            var extensionProps = t.Attributes.Where(kvp =>
                                !kvp.Key.StartsWith("script") && !kvp.Key.StartsWith("resultVariable") && !kvp.Key.StartsWith("potentialOwner")).ToList();
                            if (extensionProps.Any())
                            {
                                var ext = new XElement(XName.Get("extensionElements", ns));

                                // Camunda
                                if (t.Attributes.TryGetValue("camunda:assignee", out var camundaAssignee))
                                    ext.Add(new XElement(XName.Get("assignee", "http://camunda.org/schema/1.0/bpmn"), new XAttribute("value", camundaAssignee)));
                                if (t.Attributes.TryGetValue("camunda:formFields", out var camundaFormFields))
                                {
                                    var fields = JsonSerializer.Deserialize<List<dynamic>>(camundaFormFields);
                                    foreach (var field in fields)
                                        ext.Add(new XElement(XName.Get("formField", "http://camunda.org/schema/1.0/bpmn"),
                                            new XAttribute("id", field.Id), new XAttribute("name", field.Name), new XAttribute("type", field.Type)));
                                }

                                // Zeebe
                                if (t.Attributes.TryGetValue("zeebe:taskDefinition", out var zeebeTaskDef))
                                    ext.Add(new XElement(XName.Get("taskDefinition", "http://zeebe.io/schema/zeebe/1.0"), new XAttribute("type", zeebeTaskDef)));
                                if (t.Attributes.TryGetValue("zeebe:ioMapping", out var zeebeIoMapping))
                                {
                                    var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(zeebeIoMapping);
                                    var ioElement = new XElement(XName.Get("ioMapping", "http://zeebe.io/schema/zeebe/1.0"));
                                    foreach (var mapping in mappings)
                                        ioElement.Add(new XElement(XName.Get("input", "http://zeebe.io/schema/zeebe/1.0"),
                                            new XAttribute("source", mapping.Value), new XAttribute("target", mapping.Key)));
                                    ext.Add(ioElement);
                                }

                                // Activiti
                                if (t.Attributes.TryGetValue("activiti:formProperty", out var activitiFormProp))
                                {
                                    var props = JsonSerializer.Deserialize<Dictionary<string, string>>(activitiFormProp);
                                    var propElement = new XElement(XName.Get("formProperty", "http://activiti.org/bpmn"));
                                    foreach (var prop in props)
                                        propElement.Add(new XAttribute(prop.Key, prop.Value));
                                    ext.Add(propElement);
                                }

                                // Flowable
                                if (t.Attributes.TryGetValue("flowable:assignee", out var flowableAssignee))
                                    ext.Add(new XElement(XName.Get("assignee", "http://flowable.org/bpmn"), new XAttribute("value", flowableAssignee)));
                                if (t.Attributes.TryGetValue("flowable:taskListeners", out var flowableTaskListeners))
                                {
                                    var listeners = JsonSerializer.Deserialize<List<dynamic>>(flowableTaskListeners);
                                    foreach (var listener in listeners)
                                    {
                                        var listenerElement = new XElement(XName.Get("taskListener", "http://flowable.org/bpmn"),
                                            new XAttribute("event", listener.Event));
                                        if (!string.IsNullOrEmpty(listener.Class)) listenerElement.Add(new XAttribute("class", listener.Class));
                                        if (!string.IsNullOrEmpty(listener.Expression)) listenerElement.Add(new XAttribute("expression", listener.Expression));
                                        ext.Add(listenerElement);
                                    }
                                }
                                if (t.Attributes.TryGetValue("flowable:formFields", out var flowableFormFields))
                                {
                                    var fields = JsonSerializer.Deserialize<List<dynamic>>(flowableFormFields);
                                    foreach (var field in fields)
                                        ext.Add(new XElement(XName.Get("formField", "http://flowable.org/bpmn"),
                                            new XAttribute("id", field.Id), new XAttribute("name", field.Name), new XAttribute("type", field.Type)));
                                }

                                // CIB
                                if (t.Attributes.TryGetValue("cib:assignee", out var cibAssignee))
                                    ext.Add(new XElement(XName.Get("assignee", "http://cib.de/schema/bpmn"), new XAttribute("value", cibAssignee)));
                                if (t.Attributes.TryGetValue("cib:formFields", out var cibFormFields))
                                {
                                    var fields = JsonSerializer.Deserialize<List<dynamic>>(cibFormFields);
                                    foreach (var field in fields)
                                        ext.Add(new XElement(XName.Get("formField", "http://cib.de/schema/bpmn"),
                                            new XAttribute("id", field.Id), new XAttribute("name", field.Name), new XAttribute("type", field.Type)));
                                }
                                if (t.Attributes.TryGetValue("cib:connectors", out var cibConnectors))
                                {
                                    var connectors = JsonSerializer.Deserialize<List<dynamic>>(cibConnectors);
                                    foreach (var connector in connectors)
                                        ext.Add(new XElement(XName.Get("connector", "http://cib.de/schema/bpmn"),
                                            new XAttribute("id", connector.Id), new XAttribute("type", connector.Type), new XAttribute("url", connector.Url)));
                                }
                                if (t.Attributes.TryGetValue("cib:aiModules", out var cibAiModules))
                                {
                                    var aiModules = JsonSerializer.Deserialize<List<dynamic>>(cibAiModules);
                                    foreach (var aiModule in aiModules)
                                        ext.Add(new XElement(XName.Get("aiModule", "http://cib.de/schema/bpmn"),
                                            new XAttribute("type", aiModule.Type), new XAttribute("model", aiModule.Model)));
                                }

                                // jBPM
                                string jbpmActorId = null, jbpmGroupId = null;
                                if (t.Attributes.TryGetValue("jbpm:actorId", out jbpmActorId) || t.Attributes.TryGetValue("jbpm:groupId", out jbpmGroupId))
                                {
                                    var assignment = new XElement(XName.Get("assignment", "http://jbpm.org/bpmn"));
                                    if (!string.IsNullOrEmpty(jbpmActorId)) assignment.Add(new XAttribute("actorId", jbpmActorId));
                                    if (!string.IsNullOrEmpty(jbpmGroupId)) assignment.Add(new XAttribute("groupId", jbpmGroupId));
                                    ext.Add(assignment);
                                }
                                if (t.Attributes.TryGetValue("jbpm:workItemHandlers", out var jbpmWorkItemHandlers))
                                {
                                    var handlers = JsonSerializer.Deserialize<List<dynamic>>(jbpmWorkItemHandlers);
                                    foreach (var handler in handlers)
                                        ext.Add(new XElement(XName.Get("workItemHandler", "http://jbpm.org/bpmn"),
                                            new XAttribute("name", handler.Name), new XAttribute("class", handler.Class)));
                                }

                                // Osmanthus
                                string advanceType = null, advanceTarget = null, timeoutDuration = null, timeoutAction = null, pdfTemplateId = null, pdfOutput = null;
                                if (t.Attributes.TryGetValue("osmanthus:advanceType", out advanceType) || t.Attributes.TryGetValue("osmanthus:advanceTarget", out advanceTarget))
                                {
                                    var advance = new XElement(XName.Get("advance", "http://osmanthus.io/bpmn"));
                                    if (!string.IsNullOrEmpty(advanceType)) advance.Add(new XAttribute("type", advanceType));
                                    if (!string.IsNullOrEmpty(advanceTarget)) advance.Add(new XAttribute("target", advanceTarget));
                                    ext.Add(advance);
                                }
                                if (t.Attributes.TryGetValue("osmanthus:timeoutDuration", out timeoutDuration) || t.Attributes.TryGetValue("osmanthus:timeoutAction", out timeoutAction))
                                {
                                    var timeout = new XElement(XName.Get("timeout", "http://osmanthus.io/bpmn"));
                                    if (!string.IsNullOrEmpty(timeoutDuration)) timeout.Add(new XAttribute("duration", timeoutDuration));
                                    if (!string.IsNullOrEmpty(timeoutAction)) timeout.Add(new XAttribute("action", timeoutAction));
                                    ext.Add(timeout);
                                }
                                if (t.Attributes.TryGetValue("osmanthus:pdfTemplateId", out pdfTemplateId) || t.Attributes.TryGetValue("osmanthus:pdfOutput", out pdfOutput))
                                {
                                    var pdfTemplate = new XElement(XName.Get("pdfTemplate", "http://osmanthus.io/bpmn"));
                                    if (!string.IsNullOrEmpty(pdfTemplateId)) pdfTemplate.Add(new XAttribute("templateId", pdfTemplateId));
                                    if (!string.IsNullOrEmpty(pdfOutput)) pdfTemplate.Add(new XAttribute("output", pdfOutput));
                                    ext.Add(pdfTemplate);
                                }

                                // Alfresco
                                if (t.Attributes.TryGetValue("alfresco:formKey", out var alfrescoFormKey))
                                    ext.Add(new XElement(XName.Get("formKey", "http://alfresco.org/bpmn"), new XAttribute("value", alfrescoFormKey)));
                                if (t.Attributes.TryGetValue("alfresco:script", out var alfrescoScript))
                                    ext.Add(new XElement(XName.Get("scriptTask", "http://alfresco.org/bpmn"), new XAttribute("script", alfrescoScript)));

                                // Generische Erweiterungen
                                foreach (var prop in extensionProps.Where(kvp => !kvp.Key.Contains("camunda:") && !kvp.Key.Contains("zeebe:") &&
                                                                                !kvp.Key.Contains("activiti:") && !kvp.Key.Contains("flowable:") &&
                                                                                !kvp.Key.Contains("cib:") && !kvp.Key.Contains("jbpm:") &&
                                                                                !kvp.Key.Contains("osmanthus:") && !kvp.Key.Contains("alfresco:")))
                                {
                                    var nsParts = prop.Key.Split(':');
                                    if (nsParts.Length == 2)
                                    {
                                        var ns = nsParts[0];
                                        var localName = nsParts[1];
                                        ext.Add(new XElement(XName.Get(localName, ns), prop.Value));
                                    }
                                }
                                if (t.Type == "mcpServiceTask")
                                {
                                    var mcp = new XElement(XName.Get("mcpServiceTask", "http://vertexbpmn.io/mcp"));
                                    if (t.Attributes.TryGetValue("mcpServerUrl", out var serverUrl)) mcp.Add(new XAttribute("mcpServerUrl", serverUrl));
                                    if (t.Attributes.TryGetValue("mcpMethod", out var method)) mcp.Add(new XAttribute("mcpMethod", method));
                                    if (t.Attributes.TryGetValue("mcpParams", out var paramsAttr)) mcp.Add(new XAttribute("mcpParams", paramsAttr));
                                    ext.Add(mcp);
                                }
                                element.Add(ext);
                            }

                            return element;
                        }),
                        model.Gateways.Select(g => new XElement(XName.Get(g.Type, ns), new XAttribute("id", g.Id))),
                        model.Subprocesses.Select(s =>
                        {
                            var element = new XElement(XName.Get("subProcess", ns),
                                new XAttribute("id", s.Id),
                                s.IsEventSubprocess ? new XAttribute("triggeredByEvent", "true") : null,
                                s.IsTransaction ? new XAttribute("transaction", "true") : null);
                            if (s.IsMultiInstance)
                            {
                                var mi = new XElement(XName.Get("multiInstanceLoopCharacteristics", ns));
                                if (s.IsSequential) mi.Add(new XAttribute("isSequential", "true"));
                                if (s.LoopCardinality.HasValue) mi.Add(new XElement(XName.Get("loopCardinality", ns), s.LoopCardinality.Value));
                                element.Add(mi);
                            }
                            return element;
                        }),
                        model.SequenceFlows.Select(f => new XElement(XName.Get("sequenceFlow", ns),
                            new XAttribute("id", f.Id),
                            new XAttribute("sourceRef", f.SourceRef),
                            new XAttribute("targetRef", f.TargetRef))),
                        model.Lanes.Select(l => new XElement(XName.Get("lane", ns),
                            new XAttribute("id", l.Id),
                            new XAttribute("name", l.Name),
                            l.FlowNodeRefs.Select(fn => new XElement(XName.Get("flowNodeRef", ns), fn)))),
                        model.DataObjects.Select(d => new XElement(XName.Get("dataObject", ns),
                            new XAttribute("id", d.Id),
                            new XAttribute("name", d.Name))),
                        model.Associations.Select(a => new XElement(XName.Get("association", ns),
                            new XAttribute("id", a.Id),
                            new XAttribute("sourceRef", a.SourceRef),
                            new XAttribute("targetRef", a.TargetRef))),
                        model.TextAnnotations.Select(t => new XElement(XName.Get("textAnnotation", ns),
                            new XAttribute("id", t.Id),
                            new XElement(XName.Get("text", ns), t.Text))))
                    ),
                    model.Participants.Any() || model.MessageFlows.Any()
                        ? new XElement(XName.Get("collaboration", ns),
                            model.Participants.Select(p => new XElement(XName.Get("participant", ns),
                                new XAttribute("id", p.Id),
                                new XAttribute("processRef", p.ProcessRef))),
                            model.MessageFlows.Select(mf => new XElement(XName.Get("messageFlow", ns),
                                new XAttribute("id", mf.Id),
                                new XAttribute("sourceRef", mf.SourceRef),
                                new XAttribute("targetRef", mf.TargetRef))))
                        : null
                );

            return doc.ToString();
        }
    }

}