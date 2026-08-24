using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Runtime;
using VertexBPMN.Engine.Ecosystem;
using VertexBPMN.Engine.Performance;
using VertexBPMN.Engine.Security;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Engine.Parsing;

public partial class BpmnParser : IBpmnParser
{
    private readonly BpmnParserOptions _options;
    private readonly Dictionary<string, LinkedListNode<(string Key, BpmnModel Model)>> _cacheIndex = new();
    private readonly LinkedList<(string Key, BpmnModel Model)> _lru = new();
    private readonly object _cacheLock = new();

    //Observability Infrastructure
    private readonly ActivitySource? _activitySource;
    private readonly ILogger<BpmnParser> _logger;
    private static readonly ActivitySource DefaultActivitySource = new("VertexBPMN.Engine.Parsing");

    private readonly Tracer _tracer;
    private readonly Dictionary<string, XDocument> _documentCache = new();

    public BpmnParser() : this(new BpmnParserOptions(), Microsoft.Extensions.Logging.Abstractions.NullLogger<BpmnParser>.Instance, TracerProvider.Default) { }

    public BpmnParser(BpmnParserOptions options) : this(options, Microsoft.Extensions.Logging.Abstractions.NullLogger<BpmnParser>.Instance, TracerProvider.Default) { }

    public BpmnParser(BpmnParserOptions options, ILogger<BpmnParser> logger) : this(options, logger, TracerProvider.Default)
    {
    }
    public BpmnParser(ILogger<BpmnParser> logger, TracerProvider tracerProvider) : this(new BpmnParserOptions(), logger, tracerProvider)
    { }
    public BpmnParser(BpmnParserOptions options, ILogger<BpmnParser> logger, TracerProvider tracerProvider)
    {
        _options = options;

        //Initialize observability components (zero allocation when disabled)
        _activitySource = _options.EnableTracing ? (_options.TracingActivitySource ?? DefaultActivitySource) : null;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracer = tracerProvider.GetTracer("VertexBPMN");
    }

    /// <summary>
    /// Capabilities exposed by the roundtrip parser.
    /// </summary>
    public static readonly BpmnParserCapabilities Capabilities =
        new(
            SupportsStrictRoundtrip: true,
            SupportsRuntimeProjection: true,
            SupportsCollaboration: false,
            SupportsVendorNormalization: true,
            SupportsAdvancedValidation: true
        );

    private BpmnModel? TryGetCached(string xml)
    {
        if (_options.CacheSize <= 0) return null;
        var key = Hash(xml);
        lock (_cacheLock)
        {
            if (_cacheIndex.TryGetValue(key, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                return node.Value.Model;
            }
        }
        return null;
    }

    private void Cache(string xml, BpmnModel model)
    {
        if (_options.CacheSize <= 0) return;
        var key = Hash(xml);
        lock (_cacheLock)
        {
            if (_cacheIndex.ContainsKey(key)) return;
            var node = new LinkedListNode<(string, BpmnModel)>((key, model));
            _lru.AddFirst(node);
            _cacheIndex[key] = node;
            while (_lru.Count > _options.CacheSize)
            {
                var last = _lru.Last!; _lru.RemoveLast(); _cacheIndex.Remove(last.Value.Key);
            }
        }
    }

    private static string Hash(string xml)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(xml));
        return Convert.ToHexString(bytes);
    }

    public async Task<BpmnModel> ParseAsync(string xml, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(xml);

        var resourceLimiter = new BpmnResourceLimiter(_options.SecurityOptions);
        var limitResult = resourceLimiter.ValidateInputLimits(xml);
        if (!limitResult.IsValid)
        {
            throw new SecurityException(
                $"BPMN input exceeds resource limits: {string.Join("; ", limitResult.Violations)}");
        }

        if (_options.EnableSecurityValidation)
        {
            var securityValidator = new BpmnSecurityValidator(_options.SecurityOptions);
            var securityResult = securityValidator.ValidateSecurityConfiguration(xml);
            if (_options.FailOnSecurityThreat && !securityResult.IsSecure)
            {
                throw new SecurityException(
                    $"BPMN input failed security validation: {string.Join("; ", securityResult.Vulnerabilities)}");
            }
        }

        return await resourceLimiter.ExecuteWithResourceLimitsAsync(async parseToken =>
        {
            if (_options.EnableStreamingParse && xml.Length > _options.StreamingThreshold)
            {
                var streamingParser = new BpmnStreamingParser(_options);
                var streamingModel = await streamingParser.ParseAsync(xml, parseToken);
                return ApplyPostProcessing(streamingModel);
            }

            var model = await Parse(xml, parseToken);
            return ApplyPostProcessing(model);
        }, cancellationToken);
    }

    private Task<BpmnModel> Parse(string xml, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource?.StartActivity("BpmnParser.ParseAsync");

        if (TryGetCached(xml) is { } cached)
        {
            // Log cache hit (only if logging enabled)
            if (_options.EnableLogging)
                _logger.LogDebug("ParseAsync cache hit for XML hash {XmlHash}", Hash(xml)[..8]);
            return Task.FromResult(cached);
        }

        // ParseStart logging
        if (_options.EnableLogging)
        {
            _logger.LogDebug("ParseStart: RoundtripMode={RoundtripMode}, BuildRuntimeProjection={BuildRuntimeProjection}, NormalizeVendorExtensions={NormalizeVendorExtensions}",
                _options.RoundtripMode, _options.BuildRuntimeProjection, _options.NormalizeVendorExtensions);
        }

        var strict = _options.RoundtripMode == BpmnRoundtripMode.Strict;
        var rawDefinitionsAttr = strict ? new Dictionary<string, string>(StringComparer.Ordinal) : null;
        var rawProcessAttr = strict ? new Dictionary<string, string>(StringComparer.Ordinal) : null;
        var rawIncoming = strict ? new Dictionary<string, List<string>>() : null;
        var rawOutgoing = strict ? new Dictionary<string, List<string>>() : null;
        var rawCond = strict ? new Dictionary<string, (string Raw, bool WasCData)>() : null;
        var rawExtensions = strict ? new Dictionary<string, XElement>() : null;
        var rawEvDefs = strict ? new Dictionary<string, List<XElement>>() : null;
        var namespacePrefixes = strict ? new List<NamespacePrefix>() : null;
        var elementsMetadata = strict ? new Dictionary<string, ElementMetadata>() : null;
        var rawDocumentation = strict ? new Dictionary<string, List<XElement>>() : null;
        var rawGlobalElements = strict ? new List<XElement>() : null;
        var rawArtifacts = strict ? new List<XElement>() : null;
        var rawLanes = strict ? new List<XElement>() : null;
        var rawMultiInstance = strict ? new Dictionary<string, XElement>() : null; // original loop characteristics node
        var priorityAttrNs = strict ? new Dictionary<string, string>(StringComparer.Ordinal) : null; // priority attribute namespace per sequenceFlow
        var flowNodeAttributes = strict ? new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal) : null; // attribute snapshot per flow node / sequenceFlow
        var potentialOwnerExtras = new Dictionary<string, string>(StringComparer.Ordinal); // NEW
        var scriptTaskRaw = new Dictionary<string, (string? Format, string? Body, string? Result)>(StringComparer.Ordinal); // NEW

        XElement? diRoot = null;
        var diagnostics = new List<string>();
        using var secureReader = new BpmnResourceLimiter(_options.SecurityOptions)
            .CreateSecureXmlReader(xml, cancellationToken);
        var doc = XDocument.Load(secureReader, LoadOptions.PreserveWhitespace);
        var root = doc.Root!;
        var ns = root.Name.Namespace;
        if (strict)
        {
            foreach (var attr in root.Attributes()) if (attr.IsNamespaceDeclaration)
            {
                var prefix = (attr.Name.Namespace == XNamespace.None && attr.Name.LocalName == "xmlns") ? string.Empty : attr.Name.LocalName;
                namespacePrefixes!.Add(new NamespacePrefix(prefix, attr.Value, true));
            }
            foreach (var a in root.Attributes()) if (!a.IsNamespaceDeclaration) rawDefinitionsAttr![a.Name.ToString()] = a.Value;
        }

        // FIX: Vorher wurde per .FirstOrDefault() IMMER nur das erste <process>-Element
        // im Dokument geparst. Bei Collaborations mit mehreren Pools (mehrere <process>-
        // Elemente, z. B. A.4.0.bpmn mit WFP-6-1 + WFP-6-2) gingen dadurch sämtliche
        // Flow-Elemente (Tasks, Events, Gateways, Subprocesses, SequenceFlows) aller
        // Pools außer dem ersten komplett verloren. allProcesses sammelt jetzt alle
        // <process>-Elemente; "process" bleibt der primäre (erste) Prozess für
        // Metadaten wie Id/Name/Attribute – die eigentlichen Flow-Elemente aller
        // weiteren Prozesse werden weiter unten zusätzlich gewalkt und in dieselben
        // (gemeinsamen) Ergebnislisten gemerged.
        var allProcesses = doc.Descendants(ns + "process").ToList();
        var process = allProcesses.FirstOrDefault();
        if (process == null)
        {
            if (_options.StrictValidation)
                diagnostics.Add("No <process> element");
            var rawMeta0 = strict ? new BpmnRawMetadata(rawDefinitionsAttr, rawProcessAttr, OriginalXml: xml) : null;

            var empty = new BpmnModel(
                 string.Empty,
                 string.Empty,
                Array.Empty<BpmnEvent>(),
                Array.Empty<BpmnGateway>(),
                Array.Empty<BpmnSubprocess>(),
                Array.Empty<BpmnSequenceFlow>(),
                Array.Empty<BpmnTask>(),
                Array.Empty<BpmnDataObject>(),
                Array.Empty<BpmnDataObjectReference>(),
                Array.Empty<BpmnDataStore>(),
                Array.Empty<BpmnDataStoreReference>(),
                Array.Empty<BpmnProperty>(),
                Array.Empty<BpmnActivityIo>(),
                Array.Empty<BpmnMessage>(),
                Array.Empty<BpmnSignal>(),
                Array.Empty<BpmnError>(),
                Array.Empty<BpmnEscalation>(),
                diagnostics,
                RawMetadata: rawMeta0
            );
            empty.Runtime = null;
            IReadOnlyList<ValidationDiagnostic>? structured = null;
            if (strict && _options.EnableAdvancedValidation)
            {
                structured = ValidateModel(empty, _options);
            }
            if (_options.EnableAdvancedValidation &&
                _options.ThrowOnFatalValidation &&
                structured is { Count: > 0 })
            {
                MaybeThrowOnValidation(_options, structured);
            }

            empty.ValidationDiagnostics = structured;

            Cache(xml, empty);
            return Task.FromResult(empty);
        }

        rawDocumentation = (strict && !(IsLargeModel(process, _options) && _options.SkipDocumentationForLargeModels))
            ? new Dictionary<string, List<XElement>>() : null;

        rawArtifacts = (strict &&
                            !(IsLargeModel(process, _options) && _options.SkipArtifactsForLargeModels))
            ? new List<XElement>() : null;

        rawExtensions = (strict && !(IsLargeModel(process, _options) && _options.SkipExtensionsForLargeModels))
            ? new Dictionary<string, XElement>() : null;

        var pid = Intern(process.Attribute("id")?.Value ?? string.Empty);
        var pname = Intern(process.Attribute("name")?.Value ?? string.Empty);
        if (strict)
        {
            foreach (var a in process.Attributes()) rawProcessAttr![a.Name.ToString()] = a.Value;
            var docNodes = process.Elements(ns + "documentation").Concat(process.Elements("documentation"));
            foreach (var dn in docNodes)
            {
                if (!rawDocumentation!.TryGetValue("__process", out var list)) { list = new(); rawDocumentation["__process"] = list; }
                list.Add(new XElement(dn));
            }
        }

        // global elements capture
        var messages = doc.Descendants(ns + "message").ToList();
        var signals = doc.Descendants(ns + "signal").ToList();
        var errors = doc.Descendants(ns + "error").ToList();
        var escalations = doc.Descendants(ns + "escalation").ToList();
        if (strict)
            foreach (var g in messages.Concat(signals).Concat(errors).Concat(escalations))
                rawGlobalElements!.Add(new XElement(g));

        var messageModels = ParseMessages(messages);
        var signalModels = ParseSignals(signals);
        var errorModels = ParseErrors(errors);
        var escalationModels = ParseEscalations(escalations);
        // FIX: gatewaysRaw wurde bisher nur aus "process" (dem ersten Prozess) berechnet.
        // Damit Default-Flows von Gateways in weiteren Prozessen (anderen Pools) korrekt
        // erkannt werden, jetzt über alle Prozesse hinweg sammeln.
        var gatewaysRaw = allProcesses.SelectMany(p => p.Descendants())
            .Where(e => e.Name.LocalName.EndsWith("Gateway")).Select(g =>
            new
            {
                Id = Intern(g.Attribute("id")?.Value ?? string.Empty),
                Type = g.Name.LocalName,
                DefaultId = g.Attribute("default")?.Value
            }).ToList();
        var defaultIds =
            new HashSet<string>(gatewaysRaw.Select(g => g.DefaultId).Where(v => !string.IsNullOrWhiteSpace(v))!);
        var subprocessStack = new Stack<string>();
        var events = new List<BpmnEvent>();
        var gateways = new List<BpmnGateway>();
        var subprocesses = new List<BpmnSubprocess>();
        var flows = new List<BpmnSequenceFlow>();
        var tasks = new List<BpmnTask>();
        var dataObjects = new List<BpmnDataObject>();
        var dataObjectRefs = new List<BpmnDataObjectReference>();
        var dataStores = new List<BpmnDataStore>();
        var dataStoreRefs = new List<BpmnDataStoreReference>();
        var properties = new List<BpmnProperty>();
        var activityIo = new List<BpmnActivityIo>();
        var participants = new List<BpmnParticipant>();
        var lanes = new List<BpmnLane>();
        var messageFlows = new List<BpmnMessageFlow>();
        var textAnnotations = new List<BpmnTextAnnotation>();
        var associationArtifacts = new List<BpmnAssociation>();
        var groups = new List<BpmnGroup>();
        var flowNodeIds = new HashSet<string>();
        var idIndex = new HashSet<string>();
        var pendingMiConflicts = new HashSet<string>();
        var transactionIds = new HashSet<string>();
        var boundaryEvents = new List<(string Id, string? Attached)>();
        var linkThrowCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var linkCatchNames = new HashSet<string>(StringComparer.Ordinal);
        var orderCounter = 0;

        var unknownEventDefinitionDiagnostics = new List<ValidationDiagnostic>();
        void Walk(XElement parent)
        {
            foreach (var el in parent.Elements())
            {
                cancellationToken.ThrowIfCancellationRequested();
                orderCounter++;
                var local = el.Name.LocalName;
                var id = Intern(el.Attribute("id")?.Value ?? string.Empty);
                var currentSub = subprocessStack.Count > 0 ? subprocessStack.Peek() : null;
                if (!string.IsNullOrEmpty(id))
                    if (!idIndex.Add(id) && _options.StrictValidation)
                        diagnostics.Add($"Duplicate ID: {id}");
                var ext = ExtractExtensions(el, ns, strict, rawExtensions);
                switch (local)
                {
                    case "subProcess":
                    case "adHocSubProcess":
                        var isEvent = el.Attribute("triggeredByEvent")?.Value == "true";
                        var isTx = el.Attribute("transaction")?.Value == "true" || local == "transaction";
                        var loopInfo = ParseLoopLocal(el, ns, pendingMiConflicts); // capture raw loop node
                        if (strict && !string.IsNullOrEmpty(id))
                        {
                            var miNode = el.Element(ns + "multiInstanceLoopCharacteristics") ??
                                         el.Element("multiInstanceLoopCharacteristics");
                            var stdNode = el.Element(ns + "standardLoopCharacteristics") ??
                                          el.Element("standardLoopCharacteristics");
                            if (miNode != null || stdNode != null)
                                rawMultiInstance![id] = new XElement(miNode ?? stdNode);
                        }

                        subprocesses.Add(new BpmnSubprocess(id, isEvent, isTx, loopInfo.loop, currentSub, ext));
                        if (isTx && !string.IsNullOrEmpty(id)) transactionIds.Add(id);
                        flowNodeIds.Add(id);
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id,
                            el.Attribute(XName.Get("collection", "http://camunda.org/schema/1.0/bpmn")) != null,
                            el.Element(XName.Get("inputCollection", "http://zeebe.io/schema/zeebe/1.0")) != null,
                            el.Element(ns + "loopCardinality") != null || el.Element("loopCardinality") != null,
                            el.Attribute(XName.Get("elementVariable", "http://camunda.org/schema/1.0/bpmn")) != null,
                            el.Element(XName.Get("inputElement", "http://zeebe.io/schema/zeebe/1.0")) != null,
                            el.Element(XName.Get("outputElement", "http://zeebe.io/schema/zeebe/1.0")) != null);
                        subprocessStack.Push(id);
                        Walk(el);
                        subprocessStack.Pop();
                        break;
                    case "startEvent":
                    case "endEvent":
                    case "intermediateCatchEvent":
                    case "intermediateThrowEvent":
                    case "boundaryEvent":
                        var (defs, eventDefDiagnostics) = ParseEventDefinitionsWithDiagnostics(el, ns, _options);
                        unknownEventDefinitionDiagnostics.AddRange(eventDefDiagnostics);

                        var eventAttributes = BuildEventAttributes(el, ext);
                        events.Add(new BpmnEvent(id, local, defs, currentSub, eventAttributes));
                        flowNodeIds.Add(id);

                        if (local == "boundaryEvent")
                            boundaryEvents.Add((id, el.Attribute("attachedToRef")?.Value));

                        // link events tracking
                        foreach (var led in el.Elements())
                            if (led.Name.LocalName == "linkEventDefinition")
                            {
                                var lname = led.Attribute("name")?.Value;
                                if (!string.IsNullOrEmpty(lname))
                                {
                                    if (local == "intermediateThrowEvent")
                                    {
                                        linkThrowCounts.TryGetValue(lname!, out var c);
                                        linkThrowCounts[lname!] = c + 1;
                                    }
                                    else if (local == "intermediateCatchEvent")
                                    {
                                        linkCatchNames.Add(lname!);
                                    }
                                }
                            }

                        if (strict && _options.CaptureRawEventDefinitions)
                        {
                            var list = new List<XElement>();
                            foreach (var d in el.Elements())
                            {
                                // Capture standard BPMN event definitions OR vendor/unknown elements
                                if (d.Name.LocalName.EndsWith("EventDefinition", StringComparison.OrdinalIgnoreCase) ||
                                    d.Name.LocalName.Contains("EventDefinition", StringComparison.OrdinalIgnoreCase) ||
                                    (d.Name.Namespace != ns && d.Name.Namespace != XNamespace.None))
                                {
                                    list.Add(new XElement(d));
                                }
                            }

                            if (list.Count > 0) rawEvDefs![id] = list;
                        }

                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id);
                        break;
                    case var _ when local.EndsWith("Task") || local == "callActivity":
                        if (strict && !string.IsNullOrEmpty(id))
                        {
                            var miNodeT = el.Element(ns + "multiInstanceLoopCharacteristics") ?? el.Element("multiInstanceLoopCharacteristics");
                            var stdNodeT = el.Element(ns + "standardLoopCharacteristics") ?? el.Element("standardLoopCharacteristics");
                            if (miNodeT != null || stdNodeT != null) rawMultiInstance![id] = new XElement(miNodeT ?? stdNodeT);
                        }

                        if (local == "scriptTask" && !string.IsNullOrEmpty(id))
                        {
                            var fmt = el.Attribute("scriptFormat")?.Value;
                            var body = el.Element(ns + "script")?.Value ?? el.Element("script")?.Value;
                            var resVar = el.Attribute("resultVariable")?.Value;
                            scriptTaskRaw[id] = (fmt, body, resVar);
                        }

                        if (local == "userTask" && !string.IsNullOrEmpty(id))
                        {

                            IEnumerable<XElement> roles = el.Elements().Where(e =>
                                e.Name.LocalName == "potentialOwner" ||
                                (e.Name.LocalName == "resourceRole" &&
                                 (string?)e.Attribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance")) == "potentialOwner"));

                            foreach (var role in roles)
                            {
                                var formal = role
                                    .Element(ns + "resourceAssignmentExpression") ??
                                             role.Element("resourceAssignmentExpression");
                                var expr = formal?
                                    .Element(ns + "formalExpression") ??
                                           formal?.Element("formalExpression");
                                var text = expr?.Value?.Trim();
                                if (!string.IsNullOrWhiteSpace(text))
                                {

                                    potentialOwnerExtras[id] = text!;
                                }
                            }
                        }
                        var taskAttributes = BuildTaskAttributes(el, ns, ext);
                        var implementation = el.Attribute("implementation")?.Value;
                        if (local == "serviceTask" && taskAttributes?.ContainsKey("vertex:connector.type") == true)
                            implementation = "vertex:connector";
                        var task = new BpmnTask(id, local, currentSub, taskAttributes, implementation)
                        {
                            Name = el.Attribute("name")?.Value ?? string.Empty
                        };

                        if (tasks.All(x => x.Id != id)) tasks.Add(task);
                        flowNodeIds.Add(id);
                        // IO spec & associations
                        {
                            var ioSpec = el.Element(ns + "ioSpecification") ?? el.Element("ioSpecification");
                            if (ioSpec != null)
                            {
                                var dataInputs = ioSpec.Elements(ns + "dataInput").Concat(ioSpec.Elements("dataInput"))
                                    .Select(di =>
                                    {
                                        var did = Intern(di.Attribute("id")?.Value ?? string.Empty);
                                        if (string.IsNullOrEmpty(did)) return null;
                                        return new BpmnDataInput(did, di.Attribute("name")?.Value);
                                    }).OfType<BpmnDataInput>().ToList();
                                var dataOutputs = ioSpec.Elements(ns + "dataOutput")
                                    .Concat(ioSpec.Elements("dataOutput")).Select(dout =>
                                    {
                                        var oid = Intern(dout.Attribute("id")?.Value ?? string.Empty);
                                        if (string.IsNullOrEmpty(oid)) return null;
                                        return new BpmnDataOutput(oid, dout.Attribute("name")?.Value);
                                    }).OfType<BpmnDataOutput>().ToList();
                                var inputAssociations = el.Elements(ns + "dataInputAssociation")
                                    .Concat(el.Elements("dataInputAssociation")).Select(a =>
                                    {
                                        var src = a.Element(ns + "sourceRef")?.Value ?? a.Element("sourceRef")?.Value;
                                        var tgt = a.Element(ns + "targetRef")?.Value ?? a.Element("targetRef")?.Value;
                                        if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(tgt))
                                            return null;
                                        return new BpmnDataAssociation(Intern(src), Intern(tgt));
                                    }).OfType<BpmnDataAssociation>().ToList();
                                var outputAssociations = el.Elements(ns + "dataOutputAssociation")
                                    .Concat(el.Elements("dataOutputAssociation")).Select(a =>
                                    {
                                        var src = a.Element(ns + "sourceRef")?.Value ?? a.Element("sourceRef")?.Value;
                                        var tgt = a.Element(ns + "targetRef")?.Value ?? a.Element("targetRef")?.Value;
                                        if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(tgt))
                                            return null;
                                        return new BpmnDataAssociation(Intern(src), Intern(tgt));
                                    }).OfType<BpmnDataAssociation>().ToList();
                                if (dataInputs.Count > 0 || dataOutputs.Count > 0 || inputAssociations.Count > 0 ||
                                    outputAssociations.Count > 0)
                                    activityIo.Add(new BpmnActivityIo(id, dataInputs, dataOutputs, inputAssociations,
                                        outputAssociations));
                            }
                        }
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                    case var _ when local.EndsWith("Gateway"):
                        flowNodeIds.Add(id); gateways.Add(new BpmnGateway(id, local, gatewaysRaw.FirstOrDefault(g => g.Id == id)?.DefaultId, currentSub, ext));
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                    case "laneSet":
                        if (strict) rawLanes!.Add(new XElement(el)); // keep laneSet structure
                        Walk(el); // recurse into lanes
                        continue; // prevent double attribute handling
                    case "sequenceFlow":
                        int? priority = null; var prAttr = el.Attribute(XName.Get("priority", "http://vertexbpmn.io/schema/1.0")) ?? el.Attribute(XName.Get("priority", "http://camunda.org/schema/1.0/bpmn")) ?? el.Attribute("priority"); if (prAttr != null && int.TryParse(prAttr.Value, out var pVal)) priority = pVal; if (strict && prAttr != null && !string.IsNullOrEmpty(id)) priorityAttrNs![id] = prAttr.Name.NamespaceName; var condNode = el.Element(ns + "conditionExpression") ?? el.Element("conditionExpression"); var condText = condNode?.Value?.Trim(); if (!string.IsNullOrWhiteSpace(condText)) { ext ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); ext["conditionExpression"] = condText; }
                        flows.Add(new BpmnSequenceFlow(id, Intern(el.Attribute("sourceRef")?.Value ?? string.Empty), Intern(el.Attribute("targetRef")?.Value ?? string.Empty), defaultIds.Contains(id), condText, currentSub, ext, priority)); if (strict && condNode != null) { bool wasCData = condNode.Nodes().OfType<XCData>().Any(); rawCond![id] = (condNode.Value, wasCData); }
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                    case "dataObject":
                        dataObjects.Add(new BpmnDataObject(id, el.Attribute("name")?.Value));
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                    case "dataObjectReference":
                        dataObjectRefs.Add(new BpmnDataObjectReference(id, Intern(el.Attribute("dataObjectRef")?.Value ?? string.Empty)));
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                    case "dataStore":
                        dataStores.Add(new BpmnDataStore(id, el.Attribute("name")?.Value));
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                    case "dataStoreReference":
                        dataStoreRefs.Add(new BpmnDataStoreReference(id, Intern(el.Attribute("dataStoreRef")?.Value ?? string.Empty)));
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                    case "property":
                        properties.Add(new BpmnProperty(id, el.Attribute("name")?.Value));
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                    case "textAnnotation":
                        if (!_options.CaptureArtifacts && strict) { CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break; }
                        textAnnotations.Add(new BpmnTextAnnotation(id, el.Element(ns + "text")?.Value ?? el.Element("text")?.Value)); if (strict) rawArtifacts!.Add(new XElement(el));
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                    case "group":
                        if (!_options.CaptureArtifacts && strict) { CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break; }
                        groups.Add(new BpmnGroup(id, el.Attribute("categoryValueRef")?.Value)); if (strict) rawArtifacts!.Add(new XElement(el));
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                    case "association":
                        if (!_options.CaptureArtifacts && strict) { CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break; }
                        associationArtifacts.Add(new BpmnAssociation(id, el.Attribute("sourceRef")?.Value ?? string.Empty, el.Attribute("targetRef")?.Value ?? string.Empty, el.Attribute("associationDirection")?.Value)); if (strict) rawArtifacts!.Add(new XElement(el));
                        CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                    case "lane": if (strict) rawLanes!.Add(new XElement(el)); CaptureElementMeta(strict, flowNodeAttributes, rawDocumentation, elementsMetadata, orderCounter, ns, el, id); break;
                }

                //missing id diagnostic for flow nodes / sequenceFlow / artifacts
                if (strict && string.IsNullOrEmpty(id))
                {
                    // classify subset of elements requiring ids
                    if (local is "userTask" or "serviceTask" or "task" || local.EndsWith("Task") ||
                        local is "startEvent" or "endEvent" or "intermediateCatchEvent" or "intermediateThrowEvent" or "boundaryEvent" ||
                        local.EndsWith("Gateway") || local is "sequenceFlow")
                    {
                        diagnostics.Add($"Missing id on {local}");
                    }
                }
            }

        }

        Walk(process);

        // FIX: Alle weiteren <process>-Elemente (zusätzliche Pools einer Collaboration)
        // ebenfalls walken. Walk() ist eine lokale Funktion, die über dieselben
        // (oben deklarierten) Ergebnislisten schließt – die Elemente landen also
        // automatisch in denselben events/gateways/subprocesses/flows/tasks/... Listen
        // wie die des ersten Prozesses. subprocessStack ist nach jedem abgeschlossenen
        // Walk(process)-Aufruf wieder leer (Push/Pop sind symmetrisch), ein erneuter
        // Aufruf für ein weiteres Top-Level-<process>-Element ist daher unproblematisch.
        if (allProcesses.Count > 1)
        {
            if (_options.StrictValidation)
                diagnostics.Add($"Collaboration mit {allProcesses.Count} <process>-Elementen erkannt – alle wurden gemerged.");
            foreach (var otherProcess in allProcesses.Skip(1))
            {
                Walk(otherProcess);
            }
        }

        // Reference resolution diagnostics
        if (events.Count > 0)
        {
            var messageIds = new HashSet<string>(messageModels.Select(m => m.Id), StringComparer.Ordinal);
            var signalIds = new HashSet<string>(signalModels.Select(s => s.Id), StringComparer.Ordinal);
            var errorIds = new HashSet<string>(errorModels.Select(e => e.Id), StringComparer.Ordinal);
            var escalationIds = new HashSet<string>(escalationModels.Select(e => e.Id), StringComparer.Ordinal);

            var eventsSpan = AsSpanSafe(events);
            foreach (var ev in eventsSpan)
            {
                foreach (var def in ev.Definitions)
                {
                    switch (def)
                    {
                        case MessageEventDefinition m when !string.IsNullOrEmpty(m.MessageRef) && !messageIds.Contains(m.MessageRef): diagnostics.Add($"Unknown messageRef '{m.MessageRef}' at event {ev.Id}"); break;
                        case SignalEventDefinition s when !string.IsNullOrEmpty(s.SignalRef) && !signalIds.Contains(s.SignalRef): diagnostics.Add($"Unknown signalRef '{s.SignalRef}' at event {ev.Id}"); break;
                        case ErrorEventDefinition e when !string.IsNullOrEmpty(e.ErrorRef) && !errorIds.Contains(e.ErrorRef): diagnostics.Add($"Unknown errorRef '{e.ErrorRef}' at event {ev.Id}"); break;
                        case EscalationEventDefinition esc when !string.IsNullOrEmpty(esc.EscalationRef) && !escalationIds.Contains(esc.EscalationRef): diagnostics.Add($"Unknown escalationRef '{esc.EscalationRef}' at event {ev.Id}"); break;
                    }
                }
            }
        }

        // Additional validations
        foreach (var cid in pendingMiConflicts)
            if (!string.IsNullOrEmpty(cid))
                diagnostics.Add($"multi-instance conflict on {cid}");
        foreach (var f in flows.Where(f => f.IsDefault))
        {
            bool hasCond = !string.IsNullOrWhiteSpace(f.ConditionExpression) || (rawCond != null &&
                rawCond.TryGetValue(f.Id, out var rc) && !string.IsNullOrEmpty(rc.Raw));
            if (hasCond) diagnostics.Add($"Default flow {f.Id} has condition");
        }

        foreach (var ev in events.Where(e => e.Type == "endEvent"))
        {
            var hasCancel = rawEvDefs?.ContainsKey(ev.Id) == true &&
                            rawEvDefs[ev.Id].Any(x => x.Name.LocalName == "cancelEventDefinition");
            if (!hasCancel) hasCancel = ev.Definitions.OfType<CancelEventDefinition>().Any();
            if (hasCancel)
            {
                string? cursor = ev.SubprocessId;
                bool insideTx = false;
                while (cursor != null)
                {
                    var sp = subprocesses.FirstOrDefault(s => s.Id == cursor);
                    if (sp == null) break;
                    if (sp.IsTransaction)
                    {
                        insideTx = true;
                        break;
                    }

                    cursor = sp.SubprocessId;
                }

                if (!insideTx) diagnostics.Add($"Cancel end event {ev.Id} outside transaction");
            }
        }

        foreach (var ev in events.Where(e =>
                     e.Type == "endEvent" && e.Definitions.OfType<TerminateEventDefinition>().Any()))
        {
            string? cursor = ev.SubprocessId;
            bool insideTx = false;
            while (cursor != null)
            {
                var sp = subprocesses.FirstOrDefault(s => s.Id == cursor);
                if (sp == null) break;
                if (sp.IsTransaction)
                {
                    insideTx = true;
                    break;
                }

                cursor = sp.SubprocessId;
            }

            if (!insideTx) diagnostics.Add($"Terminate end event {ev.Id} outside transaction");
        }

        foreach (var gw in gateways)
            if (!flows.Any(f => f.SourceRef == gw.Id))
                diagnostics.Add($"Gateway {gw.Id} has no outgoing");
        foreach (var (bid, attached) in boundaryEvents)
        {
            if (string.IsNullOrEmpty(attached)) continue;
            if (!flowNodeIds.Contains(attached!))
                diagnostics.Add($"boundaryEvent {bid} attachedToRef {attached} missing");
        }

        foreach (var bev in events.Where(e =>
                     e.Type == "boundaryEvent" && e.Definitions.OfType<CompensationEventDefinition>().Any()))
        {
            if (elementsMetadata != null && elementsMetadata.TryGetValue(bev.Id, out var meta))
            {
                if (!meta.Attributes.TryGetValue("cancelActivity", out var val) ||
                    !string.Equals(val, "false", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add($"Boundary compensation event {bev.Id} must have cancelActivity='false'");
                }
            }

        }
        foreach (var bev in events.Where(e =>
                     e.Type == "boundaryEvent" && e.Definitions.OfType<EscalationEventDefinition>().Any()))
        {
            if (elementsMetadata != null && elementsMetadata.TryGetValue(bev.Id, out var meta))
            {
                if (!meta.Attributes.TryGetValue("escalationRef", out var es) ||
                    !string.IsNullOrEmpty(es))
                {
                    diagnostics.Add($"Unknown escalationRef fir boundaryEvent {bev.Id}");
                }
            }

        }

        foreach (var kv in linkThrowCounts)
        {
            if (kv.Value > 1) diagnostics.Add($"Multiple throw link events for {kv.Key}");
        }

        foreach (var kv in linkThrowCounts.Keys)
        {
            if (!linkCatchNames.Contains(kv)) diagnostics.Add($"Unmatched link {kv}");
        }

        foreach (var f in flows.Where(f => f.IsDefault))
        {
            var hasCond = !string.IsNullOrWhiteSpace(f.ConditionExpression) || (rawCond != null &&
                rawCond.TryGetValue(f.Id, out var rc) && !string.IsNullOrEmpty(rc.Raw));
            if (hasCond) diagnostics.Add($"Default flow {f.Id} has condition");
        }

        if (_options.StrictValidation)
        {
            foreach (var f in flows)
                if (!flowNodeIds.Contains(f.SourceRef) || !flowNodeIds.Contains(f.TargetRef))
                    diagnostics.Add($"SequenceFlow {f.Id} has invalid endpoints {f.SourceRef}->{f.TargetRef}");
            if (!events.Any(e => e.Type == "startEvent")) diagnostics.Add("No startEvent found in process");
        }

        List<BpmnShape>? shapes = null;
        List<BpmnEdge>? edges = null;
        if (_options.ParseDiagramInterchange)
        {
            var bpmndi = (XNamespace)"http://www.omg.org/spec/BPMN/20100524/DI";
            var omgdc = (XNamespace)"http://www.omg.org/spec/DD/20100524/DC";
            var omgdi = (XNamespace)"http://www.omg.org/spec/DD/20100524/DI";
            shapes = new List<BpmnShape>();
            edges = new List<BpmnEdge>();
            foreach (var shape in doc.Descendants(bpmndi + "BPMNShape"))
            {
                var id = Intern(shape.Attribute("id")?.Value ?? string.Empty);
                var bpmnElement = Intern(shape.Attribute("bpmnElement")?.Value ?? string.Empty);
                var bounds = shape.Element(omgdc + "Bounds");
                if (bounds != null && double.TryParse(bounds.Attribute("x")?.Value, out var x) &&
                    double.TryParse(bounds.Attribute("y")?.Value, out var y) &&
                    double.TryParse(bounds.Attribute("width")?.Value, out var w) &&
                    double.TryParse(bounds.Attribute("height")?.Value, out var h))
                    shapes.Add(new BpmnShape(id, bpmnElement, x, y, w, h));
            }

            foreach (var edge in doc.Descendants(bpmndi + "BPMNEdge"))
            {
                var id = Intern(edge.Attribute("id")?.Value ?? string.Empty);
                var bpmnElement = Intern(edge.Attribute("bpmnElement")?.Value ?? string.Empty);
                var wp = new List<(double X, double Y)>();
                foreach (var waypoint in edge.Elements(omgdi + "waypoint"))
                    if (double.TryParse(waypoint.Attribute("x")?.Value, out var wx) &&
                        double.TryParse(waypoint.Attribute("y")?.Value, out var wy))
                        wp.Add((wx, wy));
                edges.Add(new BpmnEdge(id, bpmnElement, wp));
            }

            if (strict && shapes.Count + edges.Count > 0 && _options.CaptureDiRaw)
                diRoot = doc.Descendants(bpmndi + "BPMNDiagram").FirstOrDefault()?.Parent;
        }

        if (subprocesses.Count > 0)
        {
            var updated = new List<BpmnSubprocess>(subprocesses.Count);
            var subprocessesSpan = CollectionsMarshal.AsSpan(subprocesses);
            var eventsSpan = CollectionsMarshal.AsSpan(events);
            var tasksSpan = CollectionsMarshal.AsSpan(tasks);
            var gatewaysSpan = CollectionsMarshal.AsSpan(gateways);
            var flowsSpan = CollectionsMarshal.AsSpan(flows);

            foreach (var sp in subprocessesSpan)
            {
                var childFlowNodes = new List<string>();

                // Span-based filtering - much faster than LINQ Where()
                foreach (var e in eventsSpan)
                    if (e.SubprocessId == sp.Id) childFlowNodes.Add(e.Id);

                foreach (var t in tasksSpan)
                    if (t.SubprocessId == sp.Id) childFlowNodes.Add(t.Id);

                foreach (var g in gatewaysSpan)
                    if (g.SubprocessId == sp.Id) childFlowNodes.Add(g.Id);

                foreach (var s2 in subprocessesSpan)
                    if (s2.SubprocessId == sp.Id) childFlowNodes.Add(s2.Id);

                var childSeqFlows = new List<string>();
                foreach (var f in flowsSpan)
                    if (f.SubprocessId == sp.Id) childSeqFlows.Add(f.Id);

                updated.Add(sp with { ChildFlowNodeIds = childFlowNodes, ChildSequenceFlowIds = childSeqFlows });
            }
            subprocesses = updated;
        }
        List<BpmnParticipant>? participantsList = participants;
        List<BpmnMessageFlow>? messageFlowsList = messageFlows;

        if (_options.EnableCollaborationParsing)
        {
            var collab = doc.Descendants(ns + "collaboration").FirstOrDefault();
            if (collab != null)
            {
                foreach (var part in collab.Elements(ns + "participant"))
                {
                    var pidAttr = part.Attribute("id")?.Value;
                    var pnameAttr = part.Attribute("name")?.Value ?? string.Empty;
                    var pref = part.Attribute("processRef")?.Value;
                    if (!string.IsNullOrEmpty(pidAttr))
                        participantsList.Add(new BpmnParticipant(pidAttr, pnameAttr, pref ?? string.Empty));
                }
                foreach (var mf in collab.Elements(ns + "messageFlow"))
                {
                    var id = mf.Attribute("id")?.Value;
                    if (string.IsNullOrEmpty(id)) continue;

                    var name = mf.Attribute("name")?.Value ?? string.Empty;
                    var src = mf.Attribute("sourceRef")?.Value ?? string.Empty;
                    var tgt = mf.Attribute("targetRef")?.Value ?? string.Empty;
                    messageFlowsList.Add(new BpmnMessageFlow(id, src, tgt, name));
                }
            }
        }

        IReadOnlyDictionary<string, string>? globalKinds = null;
        if (strict && _options.BuildGlobalElementIndex && rawGlobalElements is { Count: > 0 })
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var x in rawGlobalElements)
            {
                var idAttr = x.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(idAttr)) continue;
                var local = x.Name.LocalName; // message | signal | error | escalation
                dict[idAttr] = local;
            }
            globalKinds = dict;
        }

        var vendorNormalized = ParseVendorExtensions(strict, rawExtensions);

        if (potentialOwnerExtras.Count > 0)
        {
            // Ensure dictionary exists
            var merged = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
            if (vendorNormalized != null)
            {
                foreach (var kv in vendorNormalized)
                    merged[kv.Key] = kv.Value;
            }
            foreach (var kv in potentialOwnerExtras)
            {
                if (!merged.TryGetValue(kv.Key, out var existing))
                {
                    merged[kv.Key] = new ReadOnlyDictionary<string, string>(
                        new Dictionary<string, string>(StringComparer.Ordinal) { ["potentialOwner"] = kv.Value });
                }
                else
                {
                    // merge into existing read-only -> create mutable copy
                    var dict = new Dictionary<string, string>(existing, StringComparer.Ordinal)
                    {
                        ["potentialOwner"] = kv.Value
                    };
                    merged[kv.Key] = new ReadOnlyDictionary<string, string>(dict);
                }
            }
            vendorNormalized = merged;
        }

        var activities = tasks.Cast<object>().Concat(subprocesses);


        BpmnRawMetadata? rawMeta = null;
        if (strict)
        {
            if (_options.OptimizeStrictMemory)
            {
                if (rawIncoming is { Count: 0 }) rawIncoming = null;
                if (rawOutgoing is { Count: 0 }) rawOutgoing = null;
                if (rawCond is { Count: 0 }) rawCond = null;
                if (rawExtensions is { Count: 0 }) rawExtensions = null;
                if (rawEvDefs is { Count: 0 }) rawEvDefs = null;
                if (rawDefinitionsAttr is { Count: 0 }) rawDefinitionsAttr = null;
                if (rawProcessAttr is { Count: 0 }) rawProcessAttr = null;
                if (rawGlobalElements is { Count: 0 }) rawGlobalElements = null;
                if (rawArtifacts is { Count: 0 }) rawArtifacts = null;
                if (rawLanes is { Count: 0 }) rawLanes = null;
                if (namespacePrefixes is { Count: 0 }) namespacePrefixes = null;
                if (elementsMetadata is { Count: 0 }) elementsMetadata = null;
                if (rawDocumentation is { Count: 0 }) rawDocumentation = null;
                if (rawMultiInstance is { Count: 0 }) rawMultiInstance = null;
                if (priorityAttrNs is { Count: 0 }) priorityAttrNs = null;
                if (flowNodeAttributes is { Count: 0 }) flowNodeAttributes = null;
                if (vendorNormalized is { Count: 0 }) vendorNormalized = null;
            }

            rawMeta = new BpmnRawMetadata(rawDefinitionsAttr, rawProcessAttr,
                rawIncoming?.ToDictionary(k => k.Key, v => (IReadOnlyList<string>)v.Value),
                rawOutgoing?.ToDictionary(k => k.Key, v => (IReadOnlyList<string>)v.Value),
                rawCond?.ToDictionary(k => k.Key, v => v.Value),
                rawExtensions,
                rawEvDefs?.ToDictionary(k => k.Key, v => (IReadOnlyList<XElement>)v.Value),
                rawMultiInstance?.ToDictionary(k => k.Key, v => new XElement(v.Value)),
                priorityAttrNs?.ToDictionary(k => k.Key, v => v.Value),
                flowNodeAttributes, false,
                namespacePrefixes,
                elementsMetadata,
                rawGlobalElements,
                rawArtifacts,
                rawLanes,
                rawDocumentation?.ToDictionary(k => k.Key, v => (IReadOnlyList<XElement>)v.Value),
                RawDiRoot: diRoot,
                PartiallyDirtyElements: null,
                GlobalElementKinds: globalKinds,
                VendorNormalizedExtensions: vendorNormalized,
                OriginalXml: xml
            );
        }

        RuntimeProcessModel? runtime = null;
        if (_options.BuildRuntimeProjection)
        {
            runtime = RuntimeProjectionBuilder.Build(
                _options,
                pid,
                events, tasks, gateways, subprocesses, flows,
                vendorNormalized,
                rawMeta,
                scriptTaskRaw,
                potentialOwnerExtras
            );
        }

        var model = new BpmnModel(pid, pname, events, gateways, subprocesses, flows, tasks, dataObjects, dataObjectRefs,
            dataStores, dataStoreRefs, properties, activityIo, messageModels, signalModels, errorModels,
            escalationModels, diagnostics, shapes, edges, participants, lanes, messageFlows, textAnnotations,
            associationArtifacts, groups, Activities: activities, RawMetadata: rawMeta);
        model.Runtime = runtime; // set mutable property

        IReadOnlyList<ValidationDiagnostic>? structuredDiagnostics = null;
        if (_options.EnableAdvancedValidation)
        {
            structuredDiagnostics = ValidateModel(model, _options);

            //Merge unknown event definition diagnostics
            if (unknownEventDefinitionDiagnostics.Count > 0)
            {
                var allDiagnostics = new List<ValidationDiagnostic>(structuredDiagnostics);
                allDiagnostics.AddRange(unknownEventDefinitionDiagnostics);
                structuredDiagnostics = allDiagnostics;
            }

            //ValidationSummary logging
            if (_options.EnableLogging && structuredDiagnostics.Count > 0)
            {
                var errorCount = structuredDiagnostics.Count(d => d.Severity >= ValidationSeverity.Error);
                var warningCount = structuredDiagnostics.Count(d => d.Severity == ValidationSeverity.Warning);
                _logger.LogInformation("ValidationSummary: ProcessId={ProcessId}, Errors={ErrorCount}, Warnings={WarningCount}, TotalDiagnostics={TotalCount}",
                    pid, errorCount, warningCount, structuredDiagnostics.Count);
            }
        }

        if (_options.EnableAdvancedValidation &&
            _options.ThrowOnFatalValidation &&
            structuredDiagnostics is { Count: > 0 })
        {
            MaybeThrowOnValidation(_options, structuredDiagnostics);
        }
        model.ValidationDiagnostics = structuredDiagnostics;

        // Phase 5: ProjectionBuilt logging
        if (_options.EnableLogging && runtime != null)
        {
            _logger.LogDebug("ProjectionBuilt: ProcessId={ProcessId}, FlowNodeCount={FlowNodeCount}, SequenceFlowCount={SequenceFlowCount}, ScriptTaskCount={ScriptTaskCount}, PotentialOwnerCount={PotentialOwnerCount}",
                pid, runtime.FlowNodes.Count, runtime.SequenceFlows.Count,
                runtime.ScriptTasks?.Count ?? 0, runtime.PotentialOwners?.Count ?? 0);
        }

        // Phase 5: Add span attributes (only if tracing enabled)
        if (activity != null)
        {
            activity.SetTag("bpmn.process_id", pid);
            activity.SetTag("bpmn.node_count", (events.Count + tasks.Count + gateways.Count + subprocesses.Count).ToString());
            activity.SetTag("bpmn.flow_count", flows.Count.ToString());
            activity.SetTag("bpmn.roundtrip_mode", _options.RoundtripMode.ToString());
            activity.SetTag("bpmn.runtime_projection", _options.BuildRuntimeProjection.ToString().ToLowerInvariant());
            activity.SetTag("bpmn.vendor_normalization", _options.NormalizeVendorExtensions.ToString().ToLowerInvariant());

            if (structuredDiagnostics != null)
            {
                activity.SetTag("bpmn.validation_errors", structuredDiagnostics.Count(d => d.Severity >= ValidationSeverity.Error).ToString());
                activity.SetTag("bpmn.validation_warnings", structuredDiagnostics.Count(d => d.Severity == ValidationSeverity.Warning).ToString());
            }
            else
            {
                activity.SetTag("bpmn.validation_errors", "0");
                activity.SetTag("bpmn.validation_warnings", "0");
            }
        }

        if (_options.EnableLogging)
        {
            _logger.LogDebug("PhaseComplete: ProcessId={ProcessId}, ParsedSuccessfully=true, DiagnosticsCount={DiagnosticsCount}",
                pid, diagnostics.Count);
        }

        Cache(xml, model);

        return Task.FromResult(model);
    }

    private static void CaptureElementMeta(bool strict, Dictionary<string, IReadOnlyDictionary<string, string>>? flowNodeAttributes,
        Dictionary<string, List<XElement>>? rawDocumentation, Dictionary<string, ElementMetadata>? elementsMetadata, int orderCounter, XNamespace ns, XElement el
       , string id, bool hadCamundaCollection = false, bool hadZeebeInputCollection = false, bool hadLoopCardinality = false, bool hadCamundaElementVar = false,
        bool hadZeebeInputElement = false, bool hadZeebeOutputElement = false)
    {
        if (!strict || string.IsNullOrEmpty(id)) return;
        var attrDict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in el.Attributes())
        {
            if (a.IsNamespaceDeclaration) continue;
            attrDict[a.Name.ToString()] = a.Value;
        }

        elementsMetadata![id] = new ElementMetadata(orderCounter, el.Name.LocalName, attrDict, hadCamundaCollection,
            hadZeebeInputCollection, hadLoopCardinality, hadCamundaElementVar, hadZeebeInputElement,
            hadZeebeOutputElement);

        if (flowNodeAttributes != null) flowNodeAttributes[id] = attrDict;
        if (rawDocumentation != null)
        {
            var docs = el.Elements(ns + "documentation")
                .Concat(el.Elements("documentation")).ToList();
            if (docs.Count > 0)
            {
                if (!rawDocumentation!.TryGetValue(id, out var list))
                {
                    list = new List<XElement>();
                    rawDocumentation[id] = list;
                }

                foreach (var d in docs) list.Add(new XElement(d));
            }
        }
    }

    private static Dictionary<string, string>? BuildEventAttributes(XElement element, Dictionary<string, string>? extensionAttributes)
    {
        var attributes = extensionAttributes == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(extensionAttributes, StringComparer.OrdinalIgnoreCase);

        foreach (var name in new[] { "name", "attachedToRef", "cancelActivity", "isInterrupting", "isCompensation" })
        {
            if (element.Attribute(name)?.Value is { } value)
                attributes[name] = value;
        }

        return attributes.Count == 0 ? null : attributes;
    }

    private static Dictionary<string, string>? BuildTaskAttributes(
        XElement element, XNamespace bpmnNamespace, Dictionary<string, string>? extensionAttributes)
    {
        var attributes = extensionAttributes == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(extensionAttributes, StringComparer.OrdinalIgnoreCase);
        var extensionElements = element.Element(bpmnNamespace + "extensionElements") ?? element.Element("extensionElements");

        if (extensionElements != null)
        {
            // Vertex extensions are kept verbatim for strict roundtrips. In addition,
            // project their semantic fields into task attributes so the runtime does not
            // need to re-parse XML to evaluate connectors, decisions and IO mappings.
            foreach (var vertexExtension in extensionElements.Elements()
                         .Where(extension => VertexBpmnExtensions.IsVertexNamespace(extension.Name.NamespaceName)))
            {
                VertexBpmnExtensions.Flatten(vertexExtension, attributes);
            }

            foreach (var property in extensionElements.Descendants().Where(e => e.Name.LocalName == "property"))
            {
                var name = property.Attribute("name")?.Value;
                var value = property.Attribute("value")?.Value;
                if (!string.IsNullOrEmpty(name) && value != null)
                    attributes[name] = value;
            }

            foreach (var mcpTask in extensionElements.Descendants().Where(e => e.Name.LocalName == "mcpServiceTask"))
            {
                foreach (var attribute in mcpTask.Attributes().Where(a => !a.IsNamespaceDeclaration))
                    attributes[attribute.Name.LocalName] = attribute.Value;
            }

            const string flowableNamespace = "http://flowable.org/bpmn";
            var listeners = extensionElements
                .Descendants(XName.Get("taskListener", flowableNamespace))
                .Select(listener => new
                {
                    Event = listener.Attribute("event")?.Value ?? string.Empty,
                    Class = listener.Attribute("class")?.Value ?? string.Empty,
                    Expression = listener.Attribute("expression")?.Value ?? string.Empty
                })
                .ToList();
            if (listeners.Count > 0)
                attributes["flowable:taskListeners"] = JsonSerializer.Serialize(listeners);
        }

        if (element.Attribute("scriptFormat")?.Value is { } scriptFormat)
            attributes["scriptFormat"] = scriptFormat;
        if ((element.Element(bpmnNamespace + "script") ?? element.Element("script"))?.Value is { } script)
            attributes["script"] = script;
        if (element.Attribute("resultVariable")?.Value is { } resultVariable)
            attributes["resultVariable"] = resultVariable;

        return attributes.Count == 0 ? null : attributes;
    }

    private Dictionary<string, string>? ExtractExtensions(XElement el, XNamespace ns, bool strict, Dictionary<string, XElement>? rawExtensions)
    {
        if (!_options.PreserveUnknownExtensions) return null;
        var extParent = el.Element(ns + "extensionElements") ?? el.Element("extensionElements");
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (strict && rawExtensions != null && extParent != null)
        {
            var ownerId = el.Attribute("id")?.Value;
            if (!string.IsNullOrEmpty(ownerId))
            {
                rawExtensions[ownerId] = _options.UseLazyRawCloning ?
                    new LazyXElement(extParent).Element :
                    new XElement(extParent);
            }
        }

        string FormatQualifiedName(XElement node, string? prefix)
        {
            if (!string.IsNullOrEmpty(prefix)) return $"{prefix}:{node.Name.LocalName}";
            var namespaceUri = node.Name.Namespace.NamespaceName;
            return string.IsNullOrEmpty(namespaceUri)
                ? node.Name.LocalName
                : $"{{{namespaceUri}}}{node.Name.LocalName}";
        }

        string FormatQualifiedAttributeName(XAttribute attribute, string? prefix)
        {
            if (!string.IsNullOrEmpty(prefix)) return $"{prefix}:{attribute.Name.LocalName}";
            var namespaceUri = attribute.Name.Namespace.NamespaceName;
            return string.IsNullOrEmpty(namespaceUri)
                ? attribute.Name.LocalName
                : $"{{{namespaceUri}}}{attribute.Name.LocalName}";
        }

        void Harvest(XElement node)
        {
            var elemPrefix = node.GetPrefixOfNamespace(node.Name.Namespace);
            var elemQName = FormatQualifiedName(node, elemPrefix);
            var nonNamespaceAttributes = node.Attributes().Where(attr => !attr.IsNamespaceDeclaration).ToList();

            foreach (var attr in nonNamespaceAttributes)
            {
                // Attribute qualified name (attributes can also be namespaced).
                string? attrPrefix = attr.Name.Namespace == XNamespace.None
                    ? null
                    : node.GetPrefixOfNamespace(attr.Name.Namespace);
                var attrQName = FormatQualifiedAttributeName(attr, attrPrefix);
                dict[$"{elemQName}.{attrQName}"] = attr.Value;
            }

            if (nonNamespaceAttributes.Count == 0 && node != extParent)
            {
                dict[$"{elemQName}.__present"] = "true";
                if (!node.HasElements && !string.IsNullOrEmpty(node.Value))
                    dict[$"{elemQName}.__text"] = node.Value;
            }

            foreach (var child in node.Elements()) Harvest(child);
        }

        if (extParent != null)
        {
            foreach (var top in extParent.Elements()) Harvest(top);
        }

        // Vendor attributes may be attached directly to the BPMN element
        // (for example camunda:assignee). They are semantic extensions too and
        // must remain visible to normalized serialization and consumers.
        foreach (var attr in el.Attributes())
        {
            if (attr.IsNamespaceDeclaration ||
                attr.Name.Namespace == XNamespace.None ||
                attr.Name.Namespace == XNamespace.Xml ||
                attr.Name.Namespace == ns)
            {
                continue;
            }

            var prefix = el.GetPrefixOfNamespace(attr.Name.Namespace);
            var key = string.IsNullOrEmpty(prefix)
                ? attr.Name.ToString()
                : $"{prefix}:{attr.Name.LocalName}";
            dict[key] = attr.Value;
        }

        return dict.Count == 0 ? null : dict;
    }

    private List<BpmnMessage> ParseMessages(List<XElement> messages)
    {
        return messages.Select(m => new BpmnMessage(Intern(m.Attribute("id")?.Value ?? string.Empty), m.Attribute("name")?.Value))
            .Where(m => !string.IsNullOrEmpty(m.Id)).ToList();
    }
    private List<BpmnSignal> ParseSignals(List<XElement> signals)
    {
        return signals.Select(s => new BpmnSignal(Intern(s.Attribute("id")?.Value ?? string.Empty), s.Attribute("name")?.Value))
            .Where(s => !string.IsNullOrEmpty(s.Id)).ToList();
    }
    private List<BpmnError> ParseErrors(List<XElement> errors)
    {
        return errors.Select(e => new BpmnError(Intern(e.Attribute("id")?.Value ?? string.Empty), e.Attribute("name")?.Value, e.Attribute("errorCode")?.Value))
            .Where(e => !string.IsNullOrEmpty(e.Id)).ToList();
    }
    private List<BpmnEscalation> ParseEscalations(List<XElement> escalations)
    {
        return escalations.Select(e => new BpmnEscalation(Intern(e.Attribute("id")?.Value ?? string.Empty), e.Attribute("name")?.Value, e.Attribute("escalationCode")?.Value))
            .Where(e => !string.IsNullOrEmpty(e.Id)).ToList();
    }

    /// <summary>
    /// Extrahiert <properties>/<property>-Strukturen aus beliebigen Namespaces (z.B. camunda:properties).
    /// </summary>
    private static void ExtractPropertiesFromAnyNamespace(XElement extensionElements, IDictionary<string, string> attributes)
    {
        foreach (var propElem in extensionElements.Descendants().Where(e => e.Name.LocalName == "properties"))
        {
            foreach (var p in propElem.Elements().Where(e => e.Name.LocalName == "property"))
            {
                var nameAttr = p.Attribute("name")?.Value;
                var valueAttr = p.Attribute("value")?.Value;
                if (!string.IsNullOrEmpty(nameAttr) && valueAttr != null)
                {
                    // Letzte Definition gewinnt
                    attributes[nameAttr] = valueAttr;
                }
            }
        }
    }
    private bool IsLargeModel(XElement? processElement, BpmnParserOptions options)
    {
        if (!options.OptimizeLargeModels || options.LargeModelThreshold <= 0)
            return false;

        var elementCount = processElement?.Elements().Count() ?? 0;

        if (elementCount > options.LargeModelThreshold && options.EnableLogging)
        {
            _logger.LogDebug("Large model detected: ElementCount={ElementCount}, applying optimizations",
                elementCount);
        }

        return elementCount > options.LargeModelThreshold;
    }

    private static IReadOnlyList<ValidationDiagnostic> ValidateModel(BpmnModel model, BpmnParserOptions options)
    {

        var events = model.Events != null ? AsSpanSafe(model.Events) : Array.Empty<BpmnEvent>();
        var gateways = model.Gateways != null ? AsSpanSafe(model.Gateways) : Array.Empty<BpmnGateway>();
        var subprocesses = model.Subprocesses != null ? AsSpanSafe(model.Subprocesses) : Array.Empty<BpmnSubprocess>();
        var flows = AsSpanSafe(model.SequenceFlows);
        var tasks = model.Tasks != null ? AsSpanSafe(model.Tasks) : Array.Empty<BpmnTask>();
        var lanes = model.Lanes != null ? AsSpanSafe(model.Lanes) : Array.Empty<BpmnLane>();
        var messageFlows = model.MessageFlows != null ? AsSpanSafe(model.MessageFlows) : Array.Empty<BpmnMessageFlow>();
        var legacyDiagnostics = model.Diagnostics != null ? AsSpanSafe(model.Diagnostics) : Array.Empty<string>();
        var rawLaneElements = model.RawMetadata?.RawLanes; // Null-safe access
        var dataObjects = model.DataObjects != null ? AsSpanSafe(model.DataObjects) : Array.Empty<BpmnDataObject>();
        var dataObjectReferences = model.DataObjectReferences != null ? AsSpanSafe(model.DataObjectReferences) : Array.Empty<BpmnDataObjectReference>();
        var associations = model.Associations != null ? AsSpanSafe(model.Associations) : Array.Empty<BpmnAssociation>();
        var textAnnotations = model.TextAnnotations != null ? AsSpanSafe(model.TextAnnotations) : Array.Empty<BpmnTextAnnotation>();
        var groups = model.Groups != null ? AsSpanSafe(model.Groups) : Array.Empty<BpmnGroup>();
        var list = new List<ValidationDiagnostic>();
        list.AddRange(ValidateGatewayDefaults(
                model.Gateways ?? Array.Empty<BpmnGateway>(),
                model.SequenceFlows ?? Array.Empty<BpmnSequenceFlow>()));

        // ---- Legacy mapping block (keep existing mappings; abbreviated here) ----
        foreach (var msg in legacyDiagnostics)
        {
            if (msg.StartsWith("Duplicate ID:", StringComparison.Ordinal))
            {
                var id = msg.Substring("Duplicate ID:".Length).Trim();
                if (id.Length > 0)
                {
                    list.Add(new ValidationDiagnostic(
                        Code: "STR-DUP-ID",
                        Severity: ValidationSeverity.Error,
                        Message: $"Duplicate id '{id}' detected",
                        ElementId: id,
                        Category: "Structural"));
                }
            }
            const string miPrefix = "multi-instance conflict on ";
            if (msg.StartsWith(miPrefix, StringComparison.Ordinal))
            {
                var id = msg.Substring(miPrefix.Length).Trim();
                if (id.Length > 0 &&
                    !list.Exists(d => d.Code == "SEM-MI-CONFLICT" && d.ElementId == id))
                {
                    list.Add(new ValidationDiagnostic(
                        Code: "SEM-MI-CONFLICT",
                        Severity: ValidationSeverity.Warning,
                        Message: $"Multi-instance activity '{id}' has both loopCardinality and collection – remove one",
                        ElementId: id,
                        Category: "Semantic"));
                }
            }
            // boundary attached missing
            if (msg.StartsWith("boundaryEvent ", StringComparison.Ordinal) &&
                msg.EndsWith(" missing", StringComparison.Ordinal) &&
                msg.Contains(" attachedToRef "))
            {
                var tokens = msg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 5)
                {
                    var boundaryId = tokens[1];
                    if (boundaryId.Length > 0 &&
                        !list.Exists(d => d.Code == "REF-BOUNDARY-ATTACHED-MISSING" && d.ElementId == boundaryId))
                    {
                        list.Add(new ValidationDiagnostic(
                            Code: "REF-BOUNDARY-ATTACHED-MISSING",
                            Severity: ValidationSeverity.Error,
                            Message: $"BoundaryEvent '{boundaryId}' attachedToRef is missing",
                            ElementId: boundaryId,
                            Category: "Referential"));
                    }
                }
            }
            // Default flow with condition
            if (msg.StartsWith("Default flow ", StringComparison.Ordinal) &&
                msg.Contains(" has condition", StringComparison.Ordinal))
            {
                // We already add structured version below when iterating flows; skip here to avoid duplication.
            }
            // Global reference missing mappings
            if (msg.StartsWith("Unknown messageRef '", StringComparison.Ordinal))
            {
                if (TryExtractRef(msg, "Unknown messageRef '", "' at event ", out var refId, out var evId))
                    AddIfAbsent("REF-GLOBAL-MESSAGE-MISSING", evId, $"Event '{evId}' references unknown message '{refId}'");
            }
            else if (msg.StartsWith("Unknown signalRef '", StringComparison.Ordinal))
            {
                if (TryExtractRef(msg, "Unknown signalRef '", "' at event ", out var refId, out var evId))
                    AddIfAbsent("REF-GLOBAL-SIGNAL-MISSING", evId, $"Event '{evId}' references unknown signal '{refId}'");
            }
            else if (msg.StartsWith("Unknown errorRef '", StringComparison.Ordinal))
            {
                if (TryExtractRef(msg, "Unknown errorRef '", "' at event ", out var refId, out var evId))
                    AddIfAbsent("REF-GLOBAL-ERROR-MISSING", evId, $"Event '{evId}' references unknown error '{refId}'");
            }
            else if (msg.StartsWith("Unknown escalationRef '", StringComparison.Ordinal))
            {
                if (TryExtractRef(msg, "Unknown escalationRef '", "' at event ", out var refId, out var evId))
                    AddIfAbsent("REF-GLOBAL-ESCALATION-MISSING", evId, $"Event '{evId}' references unknown escalation '{refId}'");
            }
            // Link events
            if (msg.StartsWith("Unmatched link ", StringComparison.Ordinal))
            {
                var linkName = msg["Unmatched link ".Length..].Trim();
                if (linkName.Length > 0 &&
                    !list.Exists(d => d.Code == "SEM-LINK-UNMATCHED" && d.Message.Contains(linkName, StringComparison.Ordinal)))
                {
                    list.Add(new ValidationDiagnostic(
                        Code: "SEM-LINK-UNMATCHED",
                        Severity: ValidationSeverity.Error,
                        Message: $"Link event name '{linkName}' has no matching catch (exactly one throw & one catch required)",
                        ElementId: null,
                        Category: "Semantic"));
                }
            }
            else if (msg.StartsWith("Multiple throw link events for ", StringComparison.Ordinal))
            {
                var linkName = msg["Multiple throw link events for ".Length..].Trim();
                if (linkName.Length > 0 &&
                    !list.Exists(d => d.Code == "SEM-LINK-MULTIPLE-THROW" && d.Message.Contains(linkName, StringComparison.Ordinal)))
                {
                    list.Add(new ValidationDiagnostic(
                        Code: "SEM-LINK-MULTIPLE-THROW",
                        Severity: ValidationSeverity.Error,
                        Message: $"Multiple throw link events detected for name '{linkName}' (only one throw allowed)",
                        ElementId: null,
                        Category: "Semantic"));
                }
            }
            // Cancel / Terminate outside TX
            if (msg.StartsWith("Cancel end event ", StringComparison.Ordinal) &&
                msg.EndsWith(" outside transaction", StringComparison.Ordinal))
            {
                const string pc = "Cancel end event ";
                const string sc = " outside transaction";
                var idPart = msg.Substring(pc.Length, msg.Length - pc.Length - sc.Length).Trim();
                if (idPart.Length > 0 &&
                    !list.Exists(d => d.Code == "SEM-CANCEL-OUTSIDE-TX" && d.ElementId == idPart))
                {
                    list.Add(new ValidationDiagnostic(
                        Code: "SEM-CANCEL-OUTSIDE-TX",
                        Severity: ValidationSeverity.Warning,
                        Message: $"Cancel end event '{idPart}' is outside a transaction subprocess",
                        ElementId: idPart,
                        Category: "Semantic"));
                }
            }
            else if (msg.StartsWith("Terminate end event ", StringComparison.Ordinal) &&
                     msg.EndsWith(" outside transaction", StringComparison.Ordinal))
            {
                const string pt = "Terminate end event ";
                const string st = " outside transaction";
                var idPart = msg.Substring(pt.Length, msg.Length - pt.Length - st.Length).Trim();
                if (idPart.Length > 0 &&
                    !list.Exists(d => d.Code == "SEM-TERMINATE-OUTSIDE-TX" && d.ElementId == idPart))
                {
                    list.Add(new ValidationDiagnostic(
                        Code: "SEM-TERMINATE-OUTSIDE-TX",
                        Severity: ValidationSeverity.Warning,
                        Message: $"Terminate end event '{idPart}' is outside a transaction subprocess",
                        ElementId: idPart,
                        Category: "Semantic"));
                }
            }
            // Boundary compensation cancelActivity false
            if (msg.StartsWith("Boundary compensation event ", StringComparison.Ordinal) &&
                msg.EndsWith(" must have cancelActivity='false'", StringComparison.Ordinal))
            {
                const string pbc = "Boundary compensation event ";
                const string sbc = " must have cancelActivity='false'";
                var idPart = msg.Substring(pbc.Length, msg.Length - pbc.Length - sbc.Length).Trim();
                if (idPart.Length > 0 &&
                    !list.Exists(d => d.Code == "SEM-BOUNDARY-COMPENSATION-CANCELACTIVITY" && d.ElementId == idPart))
                {
                    list.Add(new ValidationDiagnostic(
                        Code: "SEM-BOUNDARY-COMPENSATION-CANCELACTIVITY",
                        Severity: ValidationSeverity.Error,
                        Message: $"Boundary compensation event '{idPart}' must set cancelActivity='false'",
                        ElementId: idPart,
                        Category: "Semantic"));
                }
            }
            // STR-MISSING-PROCESS
            if (msg == "No <process> element")
            {
                if (!list.Exists(d => d.Code == "STR-MISSING-PROCESS"))
                {
                    list.Add(new ValidationDiagnostic(
                        Code: "STR-MISSING-PROCESS",
                        Severity: ValidationSeverity.Error,
                        Message: "Root <process> element is missing",
                        ElementId: null,
                        Category: "Structural"));
                }
                continue;
            }

            // STR-MISSING-ID legacy: "Missing id on <type>"
            if (msg.StartsWith("Missing id on ", StringComparison.Ordinal))
            {
                var type = msg.Substring("Missing id on ".Length).Trim();
                if (type.Length == 0) type = "element";
                // Allow multiple occurrences (could be several elements)
                list.Add(new ValidationDiagnostic(
                    Code: "STR-MISSING-ID",
                    Severity: ValidationSeverity.Error,
                    Message: $"Flow node of type '{type}' is missing a required id",
                    ElementId: null,
                    Category: "Structural"));
                continue;
            }
        }

        // Helper local functions
        static bool TryExtractRef(string msg, string prefix, string mid, out string refId, out string eventId)
        {
            refId = string.Empty;
            eventId = string.Empty;
            if (!msg.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var midIdx = msg.IndexOf(mid, StringComparison.Ordinal);
            if (midIdx < 0) return false;
            refId = msg.Substring(prefix.Length, midIdx - prefix.Length);
            eventId = msg[(midIdx + mid.Length)..].Trim();
            return refId.Length > 0 && eventId.Length > 0;
        }
        void AddIfAbsent(string code, string elementId, string message)
        {
            if (!list.Exists(d => d.Code == code && d.ElementId == elementId))
            {
                list.Add(new ValidationDiagnostic(
                    Code: code,
                    Severity: ValidationSeverity.Error,
                    Message: message,
                    ElementId: elementId,
                    Category: "Referential"));
            }
        }

        // NEW: Direct missing ID validation (not just legacy conversion)
        // Check for missing IDs on elements that require them
        var elementsRequiringIds = new List<(string? Id, string Type)>();

        // Collect all elements that should have IDs
        foreach (var ev in events)
            elementsRequiringIds.Add((ev.Id, ev.Type));
        foreach (var task in tasks)
            elementsRequiringIds.Add((task.Id, task.Type));
        foreach (var gw in gateways)
            elementsRequiringIds.Add((gw.Id, gw.Type));
        foreach (var sp in subprocesses)
            elementsRequiringIds.Add((sp.Id, "subProcess"));
        foreach (var flow in flows)
            elementsRequiringIds.Add((flow.Id, "sequenceFlow"));

        // Generate STR-MISSING-ID diagnostics for elements without IDs
        foreach (var (id, type) in elementsRequiringIds)
        {
            if (string.IsNullOrEmpty(id))
            {
                list.Add(new ValidationDiagnostic(
                    Code: "STR-MISSING-ID",
                    Severity: ValidationSeverity.Error,
                    Message: $"Flow node of type '{type}' is missing a required id",
                    ElementId: null,
                    Category: "Structural"));
            }
        }

        // Build set of valid flow node ids
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in events) if (!string.IsNullOrEmpty(e.Id)) nodeIds.Add(e.Id);
        foreach (var t in tasks) if (!string.IsNullOrEmpty(t.Id)) nodeIds.Add(t.Id);
        foreach (var g in gateways) if (!string.IsNullOrEmpty(g.Id)) nodeIds.Add(g.Id);
        foreach (var sp in subprocesses) if (!string.IsNullOrEmpty(sp.Id)) nodeIds.Add(sp.Id);

        // Sequence flow endpoint & default condition rule
        foreach (var flow in flows)
        {
            if (string.IsNullOrEmpty(flow.Id)) continue;
            if (string.IsNullOrEmpty(flow.SourceRef) || !nodeIds.Contains(flow.SourceRef))
            {
                list.Add(new ValidationDiagnostic(
                    Code: "REF-SEQUENCE-ENDPOINT",
                    Severity: ValidationSeverity.Error,
                    Message: $"SequenceFlow '{flow.Id}' sourceRef '{flow.SourceRef}' not found",
                    ElementId: flow.Id,
                    Category: "Referential"));
            }
            if (string.IsNullOrEmpty(flow.TargetRef) || !nodeIds.Contains(flow.TargetRef))
            {
                list.Add(new ValidationDiagnostic(
                    Code: "REF-SEQUENCE-ENDPOINT",
                    Severity: ValidationSeverity.Error,
                    Message: $"SequenceFlow '{flow.Id}' targetRef '{flow.TargetRef}' not found",
                    ElementId: flow.Id,
                    Category: "Referential"));
            }
            if (flow.IsDefault && !string.IsNullOrWhiteSpace(flow.ConditionExpression))
            {
                list.Add(new ValidationDiagnostic(
                    Code: "SEM-DEFAULT-WITH-CONDITION",
                    Severity: ValidationSeverity.Error,
                    Message: $"Default sequenceFlow '{flow.Id}' MUST NOT have a conditionExpression",
                    ElementId: flow.Id,
                    Category: "Semantic"));
            }
        }

        // LANE FLOW NODE REF rule
        if (rawLaneElements is { Count: > 0 })
        {
            foreach (var laneEl in rawLaneElements.Where(x => x.Name.LocalName == "lane"))
            {
                var laneId = laneEl.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(laneId)) continue;

                foreach (var fnRef in laneEl.Elements().Where(e => e.Name.LocalName == "flowNodeRef"))
                {
                    var refId = fnRef.Value?.Trim();
                    if (string.IsNullOrEmpty(refId)) continue;
                    if (!nodeIds.Contains(refId) &&
                        !list.Exists(d => d.Code == "REF-LANE-FLOWNODE-MISSING" && d.ElementId == laneId && d.Message.Contains(refId, StringComparison.Ordinal)))
                    {
                        list.Add(new ValidationDiagnostic(
                            Code: "REF-LANE-FLOWNODE-MISSING",
                            Severity: ValidationSeverity.Warning,
                            Message: $"Lane '{laneId}' references unknown flow node '{refId}'",
                            ElementId: laneId,
                            Category: "Referential"));
                    }
                }
            }
        }

        // REF-DATAOBJECTREF-TARGET-MISSING
        if (dataObjectReferences.Length > 0)
        {
            var dataObjectIds = new HashSet<string>(dataObjects.Length, StringComparer.Ordinal);
            foreach (var d in dataObjects)
                if (!string.IsNullOrEmpty(d.Id)) dataObjectIds.Add(d.Id);
            foreach (var dref in dataObjectReferences)
            {
                if (string.IsNullOrEmpty(dref.Id)) continue;
                if (string.IsNullOrEmpty(dref.DataObjectRef) || !dataObjectIds.Contains(dref.DataObjectRef))
                {
                    if (!list.Exists(d => d.Code == "REF-DATAOBJECTREF-TARGET-MISSING" && d.ElementId == dref.Id))
                    {
                        list.Add(new ValidationDiagnostic(
                            Code: "REF-DATAOBJECTREF-TARGET-MISSING",
                            Severity: ValidationSeverity.Error,
                            Message: $"DataObjectReference '{dref.Id}' references missing dataObject '{dref.DataObjectRef}'",
                            ElementId: dref.Id,
                            Category: "Referential"));
                    }
                }
            }
        }

        // REF-ASSOCIATION-ENDPOINT-MISSING (Warning for each missing endpoint)
        if (associations.Length > 0)
        {
            // Build a set of "known" artifact/flow node ids that associations could legally reference.
            var knownIds = new HashSet<string>(nodeIds, StringComparer.Ordinal);
            foreach (var d in dataObjects) if (!string.IsNullOrEmpty(d.Id)) knownIds.Add(d.Id);
            foreach (var tn in textAnnotations) if (!string.IsNullOrEmpty(tn.Id)) knownIds.Add(tn.Id);
            foreach (var g in groups) if (!string.IsNullOrEmpty(g.Id)) knownIds.Add(g.Id);
            foreach (var dref in dataObjectReferences) if (!string.IsNullOrEmpty(dref.Id)) knownIds.Add(dref.Id);

            foreach (var assoc in associations)
            {
                if (string.IsNullOrEmpty(assoc.Id)) continue;

                var missingSource = string.IsNullOrEmpty(assoc.SourceRef) || !knownIds.Contains(assoc.SourceRef);
                var missingTarget = string.IsNullOrEmpty(assoc.TargetRef) || !knownIds.Contains(assoc.TargetRef);

                if (missingSource)
                {
                    if (!list.Exists(d => d.Code == "REF-ASSOCIATION-ENDPOINT-MISSING" && d.ElementId == assoc.Id && d.Message.Contains("sourceRef", StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(new ValidationDiagnostic(
                            Code: "REF-ASSOCIATION-ENDPOINT-MISSING",
                            Severity: ValidationSeverity.Warning,
                            Message: $"Association '{assoc.Id}' sourceRef '{assoc.SourceRef}' not found",
                            ElementId: assoc.Id,
                            Category: "Referential"));
                    }
                }
                if (missingTarget)
                {
                    if (!list.Exists(d => d.Code == "REF-ASSOCIATION-ENDPOINT-MISSING" && d.ElementId == assoc.Id && d.Message.Contains("targetRef", StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(new ValidationDiagnostic(
                            Code: "REF-ASSOCIATION-ENDPOINT-MISSING",
                            Severity: ValidationSeverity.Warning,
                            Message: $"Association '{assoc.Id}' targetRef '{assoc.TargetRef}' not found",
                            ElementId: assoc.Id,
                            Category: "Referential"));
                    }
                }
            }
        }

        // SEM-EVENTSUBPROCESS-START-TYPE:
        // Validate startEvent definitions inside event subprocesses (triggeredByEvent="true").
        if (subprocesses.Length > 0)
        {
            // Allowed event definition CLR types
            static bool IsAllowed(EventDefinition def) =>
                def is MessageEventDefinition
                 or TimerEventDefinition
                 or EscalationEventDefinition
                 or ErrorEventDefinition
                 or CompensationEventDefinition
                 or SignalEventDefinition
                 or ConditionalEventDefinition;

            // Build event subprocess IDs set using spans - much faster than LINQ
            var eventSubIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sp in subprocesses)
            {
                if (sp.IsEventSubprocess && !string.IsNullOrEmpty(sp.Id))
                    eventSubIds.Add(sp.Id);
            }

            if (eventSubIds.Count > 0)
            {
                // Span-based filtering instead of LINQ Where() chain
                foreach (var ev in events)
                {
                    // Manual filtering conditions - much faster than LINQ Where()
                    if (ev.Type != "startEvent" ||
                        string.IsNullOrEmpty(ev.SubprocessId) ||
                        !eventSubIds.Contains(ev.SubprocessId))
                        continue;

                    if (string.IsNullOrEmpty(ev.Id)) continue;

                    if (ev.Definitions.Count == 0)
                    {
                        AddStartTypeDiagnostic(ev, "none");
                        continue;
                    }

                    var invalid = false;
                    string? badType = null;
                    foreach (var def in ev.Definitions)
                    {
                        if (!IsAllowed(def))
                        {
                            invalid = true;
                            badType = def.GetType().Name.Replace("EventDefinition", "", StringComparison.Ordinal);
                            break;
                        }
                    }
                    if (invalid)
                    {
                        AddStartTypeDiagnostic(ev, badType ?? "unknown");
                    }
                }

                void AddStartTypeDiagnostic(BpmnEvent e, string problem)
                {
                    var normalized = problem.ToLowerInvariant();
                    if (!list.Exists(d => d.Code == "SEM-EVENTSUBPROCESS-START-TYPE" && d.ElementId == e.Id))
                    {
                        list.Add(new ValidationDiagnostic(
                            Code: "SEM-EVENTSUBPROCESS-START-TYPE",
                            Severity: ValidationSeverity.Error,
                            Message: $"Event subprocess start event '{e.Id}' has invalid start type '{normalized}'",
                            ElementId: e.Id,
                            Category: "Semantic"));
                    }
                }
            }
        }
        // SEM-EVENTGW-INVALID-OUTGOING:
        // For each event-based gateway, all outgoing targets must be catching intermediate events (intermediateCatchEvent).
        if (gateways.Length > 0)
        {
            // Build quick lookup for events by id and type
            var eventTypeById = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var ev in events)
            {
                if (!string.IsNullOrEmpty(ev.Id))
                    eventTypeById[ev.Id] = ev.Type;
            }

            // Group flows by source for efficiency
            var flowsBySource = new Dictionary<string, List<BpmnSequenceFlow>>(StringComparer.Ordinal);
            foreach (var f in flows)
            {
                if (string.IsNullOrEmpty(f.SourceRef)) continue;
                if (!flowsBySource.TryGetValue(f.SourceRef, out var lst))
                {
                    lst = new List<BpmnSequenceFlow>();
                    flowsBySource[f.SourceRef] = lst;
                }
                lst.Add(f);
            }

            // ✅ SPAN-OPTIMIZED: Replace LINQ Where() with direct span iteration
            foreach (var gw in gateways)
            {
                if (!string.Equals(gw.Type, "eventBasedGateway", StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(gw.Id))
                    continue;

                if (!flowsBySource.TryGetValue(gw.Id, out var outgoing)) continue;

                foreach (var flow in outgoing)
                {
                    var tgt = flow.TargetRef;
                    if (string.IsNullOrEmpty(tgt))
                    {
                        // Endpoint rule already covers missing target; skip duplication
                        continue;
                    }

                    // Valid only if target is an intermediateCatchEvent
                    if (!eventTypeById.TryGetValue(tgt, out var tType) || !string.Equals(tType, "intermediateCatchEvent", StringComparison.Ordinal))
                    {
                        if (!list.Exists(d => d.Code == "SEM-EVENTGW-INVALID-OUTGOING" && d.ElementId == gw.Id && d.Message.Contains(tgt, StringComparison.Ordinal)))
                        {
                            list.Add(new ValidationDiagnostic(
                                Code: "SEM-EVENTGW-INVALID-OUTGOING",
                                Severity: ValidationSeverity.Error,
                                Message: $"Event-based gateway '{gw.Id}' has outgoing flow '{flow.Id}' to invalid target '{tgt}' (must target intermediateCatchEvent)",
                                ElementId: gw.Id,
                                Category: "Semantic"));
                        }
                    }
                }
            }
        }
        // --
        // - Advisory Reachability Rules (BFS from start events) ---
        // Build adjacency once
        if (events.Length > 0 || tasks.Length > 0 || gateways.Length > 0 || subprocesses.Length > 0)
        {
            var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var f in flows)
            {
                if (string.IsNullOrEmpty(f.SourceRef) || string.IsNullOrEmpty(f.TargetRef)) continue;
                if (!adjacency.TryGetValue(f.SourceRef, out var listTargets))
                {
                    listTargets = new List<string>();
                    adjacency[f.SourceRef] = listTargets;
                }
                listTargets.Add(f.TargetRef);
            }

            // ✅ SPAN-OPTIMIZED: Start events only at root level (no SubprocessId) for top-level reachability
            var startIds = new List<string>();
            foreach (var e in events)
            {
                if (e.Type == "startEvent" &&
                    string.IsNullOrEmpty(e.SubprocessId) &&
                    !string.IsNullOrEmpty(e.Id))
                {
                    startIds.Add(e.Id);
                }
            }

            var reachable = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();

            foreach (var s in startIds)
            {
                if (reachable.Add(s)) queue.Enqueue(s);
            }

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (adjacency.TryGetValue(cur, out var outs))
                {
                    foreach (var tgt in outs)
                    {
                        if (!string.IsNullOrEmpty(tgt) && reachable.Add(tgt))
                            queue.Enqueue(tgt);
                    }
                }
            }

            // Node set already built: nodeIds
            if (reachable.Count > 0)
            {
                foreach (var nid in nodeIds)
                {
                    if (!reachable.Contains(nid))
                    {
                        // Unreachable node
                        if (!list.Exists(d => d.Code == "ADV-UNREACHABLE-NODE" && d.ElementId == nid))
                        {
                            list.Add(new ValidationDiagnostic(
                                Code: "ADV-UNREACHABLE-NODE",
                                Severity: ValidationSeverity.Info,
                                Message: $"Flow node '{nid}' is unreachable from any start event",
                                ElementId: nid,
                                Category: "Advisory"));
                        }
                    }
                }

                // ✅ SPAN-OPTIMIZED: Orphaned End Events (subset of unreachable end events)
                foreach (var end in events)
                {
                    if (end.Type == "endEvent" &&
                        !string.IsNullOrEmpty(end.Id) &&
                        !reachable.Contains(end.Id) &&
                        !list.Exists(d => d.Code == "ADV-ORPHANED-END" && d.ElementId == end.Id))
                    {
                        list.Add(new ValidationDiagnostic(
                            Code: "ADV-ORPHANED-END",
                            Severity: ValidationSeverity.Info,
                            Message: $"End event '{end.Id}' is not reachable (orphaned)",
                            ElementId: end.Id,
                            Category: "Advisory"));
                    }
                }

                // Dead sequence flows: endpoints include a node that is unreachable
                foreach (var f in flows)
                {
                    if (string.IsNullOrEmpty(f.Id)) continue;
                    var dead = string.IsNullOrEmpty(f.SourceRef) || string.IsNullOrEmpty(f.TargetRef)
                                                                 || !reachable.Contains(f.SourceRef) || !reachable.Contains(f.TargetRef);
                    if (dead && !list.Exists(d => d.Code == "ADV-DEAD-SEQUENCE-FLOW" && d.ElementId == f.Id))
                    {
                        list.Add(new ValidationDiagnostic(
                            Code: "ADV-DEAD-SEQUENCE-FLOW",
                            Severity: ValidationSeverity.Info,
                            Message: $"SequenceFlow '{f.Id}' is dead (one or both endpoints unreachable)",
                            ElementId: f.Id,
                            Category: "Advisory"));
                    }
                }
            }
        }
        VertexBpmnExtensions.Validate(model, list);
        return list;
    }

    private static IEnumerable<ValidationDiagnostic> ValidateGatewayDefaults(
    IReadOnlyList<BpmnGateway> gateways,
    IReadOnlyList<BpmnSequenceFlow> flows)
    {
        var result = new List<ValidationDiagnostic>();

        var flowsById = flows
            .Where(flow => !string.IsNullOrWhiteSpace(flow.Id))
            .GroupBy(flow => flow.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Single());

        foreach (var gateway in gateways)
        {
            if (string.IsNullOrWhiteSpace(gateway.DefaultFlowId))
            {
                continue;
            }

            if (!flowsById.TryGetValue(gateway.DefaultFlowId, out var defaultFlow))
            {
                result.Add(new ValidationDiagnostic(
                    Code: "REF-GATEWAY-DEFAULT-MISSING",
                    Severity: ValidationSeverity.Error,
                    Message:
                        $"Gateway '{gateway.Id}' references missing default SequenceFlow '{gateway.DefaultFlowId}'.",
                    ElementId: gateway.Id,
                    Category: "Referential"));

                continue;
            }

            if (!string.Equals(defaultFlow.SourceRef, gateway.Id, StringComparison.Ordinal))
            {
                result.Add(new ValidationDiagnostic(
                    Code: "SEM-GATEWAY-DEFAULT-SOURCE",
                    Severity: ValidationSeverity.Error,
                    Message:
                        $"Default SequenceFlow '{defaultFlow.Id}' does not originate at Gateway '{gateway.Id}'.",
                    ElementId: gateway.Id,
                    Category: "Semantic"));
            }

            if (!defaultFlow.IsDefault)
            {
                result.Add(new ValidationDiagnostic(
                    Code: "SEM-GATEWAY-DEFAULT-FLAG",
                    Severity: ValidationSeverity.Error,
                    Message:
                        $"SequenceFlow '{defaultFlow.Id}' is referenced as default by Gateway '{gateway.Id}', " +
                        "but is not marked as default in the model.",
                    ElementId: gateway.Id,
                    Category: "Semantic"));
            }

            if (!string.IsNullOrWhiteSpace(defaultFlow.ConditionExpression))
            {
                result.Add(new ValidationDiagnostic(
                    Code: "SEM-DEFAULT-WITH-CONDITION",
                    Severity: ValidationSeverity.Error,
                    Message:
                        $"Default SequenceFlow '{defaultFlow.Id}' must not have a conditionExpression.",
                    ElementId: defaultFlow.Id,
                    Category: "Semantic"));
            }
        }

        return result;
    }
    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? ParseVendorExtensions(bool strict, Dictionary<string, XElement>? rawExtensions)
    {
        // vendor extension normalization capture (expanded all vendors + generics)
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? vendorNormalized = null;
        if (strict && _options.NormalizeVendorExtensions && rawExtensions is { Count: > 0 })
        {
            var camundaNs = "http://camunda.org/schema/1.0/bpmn";
            var zeebeNs = "http://zeebe.io/schema/zeebe/1.0";
            var flowableNs = "http://flowable.org/bpmn";
            var activitiNs = "http://activiti.org/bpmn";
            var cibNs = "http://cib.de/schema/bpmn";
            var jbpmNs = "http://jbpm.org/bpmn";
            var osmanthusNs = "http://osmanthus.io/bpmn";
            var alfrescoNs = "http://alfresco.org/bpmn";
            var mcpNs = "http://vertexbpmn.io/mcp";

            var knownSet = new HashSet<string>(StringComparer.Ordinal)
            {
                camundaNs, zeebeNs, flowableNs, activitiNs, cibNs, jbpmNs, osmanthusNs, alfrescoNs, mcpNs
            };

            static string NextIndexedKey(Dictionary<string, string> bucket, string baseKey)
            {
                if (!bucket.ContainsKey(baseKey + "#1"))
                {
                    return baseKey + "#1";
                }
                int i = 2;
                while (bucket.ContainsKey(baseKey + "#" + i)) i++;
                return baseKey + "#" + i;
            }

            var tmp = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

            foreach (var kv in rawExtensions)
            {
                var ownerId = kv.Key;
                if (string.IsNullOrEmpty(ownerId)) continue;
                var root0 = kv.Value;
                var bucket = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (var child in root0.Elements())
                {
                    var nsUri = child.Name.NamespaceName;
                    var local = child.Name.LocalName;

                    // CAMUNDA
                    if (nsUri == camundaNs)
                    {
                        switch (local)
                        {
                            case "assignee":
                                if (child.Attribute("value")?.Value is { } cav && cav.Length > 0)
                                    bucket["camunda:assignee"] = cav;
                                break;
                            case "formField":
                                {
                                    var fid = child.Attribute("id")?.Value;
                                    if (!string.IsNullOrEmpty(fid))
                                    {
                                        var fType = child.Attribute("type")?.Value;
                                        var fName = child.Attribute("name")?.Value;
                                        if (!string.IsNullOrEmpty(fType))
                                            bucket[$"camunda:formField.{fid}.type"] = fType;
                                        if (!string.IsNullOrEmpty(fName))
                                            bucket[$"camunda:formField.{fid}.name"] = fName;
                                    }
                                    break;
                                }
                            case "formData":
                                {

                                    bucket["camunda:formData"] = child.Name.LocalName;

                                    child.Elements().ToList().ForEach(ff =>
                                    {
                                        var fid = ff.Attribute("id")?.Value;
                                        if (!string.IsNullOrEmpty(fid))
                                        {
                                            var fType = ff.Attribute("type")?.Value;
                                            var fName = ff.Attribute("name")?.Value;
                                            var flabel = ff.Attribute("label")?.Value;
                                            if (!string.IsNullOrEmpty(fType))
                                                bucket[$"camunda:formField.{fid}.type"] = fType;
                                            if (!string.IsNullOrEmpty(fName))
                                                bucket[$"camunda:formField.{fid}.name"] = fName;
                                            if (!string.IsNullOrEmpty(flabel))
                                                bucket[$"camunda:formField.{fid}.label"] = flabel;
                                        }
                                    });
                                    break;
                                }
                            case "properties":
                                foreach (var prop in child.Elements(XName.Get("property", camundaNs)))
                                {
                                    var pname = prop.Attribute("name")?.Value;
                                    var pval = prop.Attribute("value")?.Value;
                                    if (!string.IsNullOrEmpty(pname) && pval != null)
                                        bucket[$"camunda:property.{pname}"] = pval;
                                }
                                break;
                            case "taskListener":
                                {
                                    var baseKey = "camunda:taskListener";
                                    var idxKey = NextIndexedKey(bucket, baseKey);
                                    var ev = child.Attribute("event")?.Value;
                                    var clazz = child.Attribute("class")?.Value;
                                    var expr = child.Attribute("expression")?.Value;
                                    if (!string.IsNullOrEmpty(ev)) bucket[$"{idxKey}.event"] = ev;
                                    if (!string.IsNullOrEmpty(clazz)) bucket[$"{idxKey}.class"] = clazz;
                                    if (!string.IsNullOrEmpty(expr)) bucket[$"{idxKey}.expression"] = expr;
                                    break;
                                }
                        }
                    }
                    // ZEEBE
                    else if (nsUri == zeebeNs)
                    {
                        switch (local)
                        {
                            case "taskDefinition":
                                if (child.Attribute("type")?.Value is { } t && t.Length > 0)
                                    bucket["zeebe:taskDefinition.type"] = t;
                                break;
                            case "ioMapping":
                                foreach (var input in child.Elements(XName.Get("input", zeebeNs)))
                                {
                                    var src = input.Attribute("source")?.Value;
                                    var tgt = input.Attribute("target")?.Value;
                                    if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(tgt))
                                        bucket[$"zeebe:ioMapping.input.{tgt}"] = src;
                                }
                                foreach (var output in child.Elements(XName.Get("output", zeebeNs)))
                                {
                                    var src = output.Attribute("source")?.Value;
                                    var tgt = output.Attribute("target")?.Value;
                                    if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(tgt))
                                        bucket[$"zeebe:ioMapping.output.{tgt}"] = src;
                                }
                                break;
                            case "taskHeaders":
                                foreach (var header in child.Elements(XName.Get("header", zeebeNs)))
                                {
                                    var hKey = header.Attribute("key")?.Value;
                                    var hVal = header.Attribute("value")?.Value;
                                    if (!string.IsNullOrEmpty(hKey) && hVal != null)
                                        bucket[$"zeebe:taskHeaders.{hKey}"] = hVal;
                                }
                                break;
                        }
                    }
                    // FLOWABLE
                    else if (nsUri == flowableNs)
                    {
                        switch (local)
                        {
                            case "assignee":
                                if (child.Attribute("value")?.Value is { } fav && fav.Length > 0)
                                    bucket["flowable:assignee"] = fav;
                                break;
                            case "formField":
                                {
                                    var fid = child.Attribute("id")?.Value;
                                    if (!string.IsNullOrEmpty(fid))
                                    {
                                        var fType = child.Attribute("type")?.Value;
                                        var fName = child.Attribute("name")?.Value;
                                        if (!string.IsNullOrEmpty(fType))
                                            bucket[$"flowable:formField.{fid}.type"] = fType;
                                        if (!string.IsNullOrEmpty(fName))
                                            bucket[$"flowable:formField.{fid}.name"] = fName;
                                    }
                                    break;
                                }
                            case "taskListener":
                                {
                                    var baseKey = "flowable:taskListener";
                                    var idxKey = NextIndexedKey(bucket, baseKey);
                                    var ev = child.Attribute("event")?.Value;
                                    var clazz = child.Attribute("class")?.Value;
                                    var expr = child.Attribute("expression")?.Value;
                                    if (!string.IsNullOrEmpty(ev)) bucket[$"{idxKey}.event"] = ev;
                                    if (!string.IsNullOrEmpty(clazz)) bucket[$"{idxKey}.class"] = clazz;
                                    if (!string.IsNullOrEmpty(expr)) bucket[$"{idxKey}.expression"] = expr;
                                    break;
                                }
                        }
                    }
                    // ACTIVITI
                    else if (nsUri == activitiNs)
                    {
                        switch (local)
                        {
                            case "formProperty":
                                {
                                    var fid = child.Attribute("id")?.Value;
                                    if (!string.IsNullOrEmpty(fid))
                                    {
                                        var fType = child.Attribute("type")?.Value;
                                        var fName = child.Attribute("name")?.Value;
                                        if (!string.IsNullOrEmpty(fType))
                                            bucket[$"activiti:formProperty.{fid}.type"] = fType;
                                        if (!string.IsNullOrEmpty(fName))
                                            bucket[$"activiti:formProperty.{fid}.name"] = fName;
                                        foreach (var attr in child.Attributes())
                                        {
                                            if (attr.IsNamespaceDeclaration) continue;
                                            if (attr.Name.LocalName is "id" or "type" or "name") continue;
                                            if (attr.Value.Length == 0) continue;
                                            bucket[$"activiti:formProperty.{fid}.{attr.Name.LocalName}"] = attr.Value;
                                        }
                                    }
                                    break;
                                }
                            case "taskListener":
                                {
                                    var baseKey = "activiti:taskListener";
                                    var idxKey = NextIndexedKey(bucket, baseKey);
                                    var ev = child.Attribute("event")?.Value;
                                    var clazz = child.Attribute("class")?.Value;
                                    var expr = child.Attribute("expression")?.Value;
                                    var del = child.Attribute("delegateExpression")?.Value;
                                    if (!string.IsNullOrEmpty(ev)) bucket[$"{idxKey}.event"] = ev;
                                    if (!string.IsNullOrEmpty(clazz)) bucket[$"{idxKey}.class"] = clazz;
                                    if (!string.IsNullOrEmpty(expr)) bucket[$"{idxKey}.expression"] = expr;
                                    if (!string.IsNullOrEmpty(del)) bucket[$"{idxKey}.delegateExpression"] = del;
                                    break;
                                }
                            case "executionListener":
                                {
                                    var baseKey = "activiti:executionListener";
                                    var idxKey = NextIndexedKey(bucket, baseKey);
                                    var ev = child.Attribute("event")?.Value;
                                    var clazz = child.Attribute("class")?.Value;
                                    var expr = child.Attribute("expression")?.Value;
                                    var del = child.Attribute("delegateExpression")?.Value;
                                    if (!string.IsNullOrEmpty(ev)) bucket[$"{idxKey}.event"] = ev;
                                    if (!string.IsNullOrEmpty(clazz)) bucket[$"{idxKey}.class"] = clazz;
                                    if (!string.IsNullOrEmpty(expr)) bucket[$"{idxKey}.expression"] = expr;
                                    if (!string.IsNullOrEmpty(del)) bucket[$"{idxKey}.delegateExpression"] = del;
                                    break;
                                }
                            case "candidateUsers":
                                if (child.Attribute("value")?.Value is { } cu && cu.Length > 0)
                                    bucket["activiti:candidateUsers"] = cu;
                                break;
                            case "candidateGroups":
                                if (child.Attribute("value")?.Value is { } cg && cg.Length > 0)
                                    bucket["activiti:candidateGroups"] = cg;
                                break;
                        }
                    }
                    // CIB
                    else if (nsUri == cibNs)
                    {
                        switch (local)
                        {
                            case "assignee":
                                if (child.Attribute("value")?.Value is { } cav && cav.Length > 0)
                                    bucket["cib:assignee"] = cav;
                                break;
                            case "formField":
                                {
                                    var fid = child.Attribute("id")?.Value;
                                    if (!string.IsNullOrEmpty(fid))
                                    {
                                        var fType = child.Attribute("type")?.Value;
                                        var fName = child.Attribute("name")?.Value;
                                        if (!string.IsNullOrEmpty(fType))
                                            bucket[$"cib:formField.{fid}.type"] = fType;
                                        if (!string.IsNullOrEmpty(fName))
                                            bucket[$"cib:formField.{fid}.name"] = fName;
                                    }
                                    break;
                                }
                            case "connector":
                                {
                                    var cid = child.Attribute("id")?.Value;
                                    if (string.IsNullOrEmpty(cid))
                                        cid = NextIndexedKey(bucket, "cib:connector").Split('#')[1]; // fallback index part
                                    var idKey = string.IsNullOrEmpty(child.Attribute("id")?.Value) ? NextIndexedKey(bucket, "cib:connector") : $"cib:connector.{cid}";
                                    var realKeyPrefix = idKey.StartsWith("cib:connector#") ? idKey : $"cib:connector.{cid}";
                                    var cType = child.Attribute("type")?.Value;
                                    var url = child.Attribute("url")?.Value;
                                    if (!string.IsNullOrEmpty(cType)) bucket[$"{realKeyPrefix}.type"] = cType;
                                    if (!string.IsNullOrEmpty(url)) bucket[$"{realKeyPrefix}.url"] = url;
                                    break;
                                }
                            case "aiModule":
                                {
                                    var type = child.Attribute("type")?.Value;
                                    var model = child.Attribute("model")?.Value;
                                    string keyPrefix;
                                    if (!string.IsNullOrEmpty(type))
                                        keyPrefix = $"cib:aiModule.{type}";
                                    else
                                        keyPrefix = NextIndexedKey(bucket, "cib:aiModule");
                                    if (!string.IsNullOrEmpty(model))
                                        bucket[$"{keyPrefix}.model"] = model;
                                    break;
                                }
                        }
                    }
                    // JBPM
                    else if (nsUri == jbpmNs)
                    {
                        switch (local)
                        {
                            case "assignment":
                                {
                                    var actor = child.Attribute("actorId")?.Value;
                                    var grp = child.Attribute("groupId")?.Value;
                                    if (!string.IsNullOrEmpty(actor)) bucket["jbpm:assignment.actorId"] = actor;
                                    if (!string.IsNullOrEmpty(grp)) bucket["jbpm:assignment.groupId"] = grp;
                                    break;
                                }
                            case "workItemHandler":
                                {
                                    var name = child.Attribute("name")?.Value;
                                    var cls = child.Attribute("class")?.Value;
                                    string keyPrefix;
                                    if (!string.IsNullOrEmpty(name))
                                        keyPrefix = $"jbpm:workItemHandler.{name}";
                                    else
                                        keyPrefix = NextIndexedKey(bucket, "jbpm:workItemHandler");
                                    if (!string.IsNullOrEmpty(cls))
                                        bucket[$"{keyPrefix}.class"] = cls;
                                    break;
                                }
                        }
                    }
                    // OSMANTHUS
                    else if (nsUri == osmanthusNs)
                    {
                        switch (local)
                        {
                            case "advance":
                                {
                                    var type = child.Attribute("type")?.Value;
                                    var target = child.Attribute("target")?.Value;
                                    if (!string.IsNullOrEmpty(type)) bucket["osmanthus:advance.type"] = type;
                                    if (!string.IsNullOrEmpty(target)) bucket["osmanthus:advance.target"] = target;
                                    break;
                                }
                            case "timeout":
                                {
                                    var dur = child.Attribute("duration")?.Value;
                                    var act = child.Attribute("action")?.Value;
                                    if (!string.IsNullOrEmpty(dur)) bucket["osmanthus:timeout.duration"] = dur;
                                    if (!string.IsNullOrEmpty(act)) bucket["osmanthus:timeout.action"] = act;
                                    break;
                                }
                            case "pdfTemplate":
                                {
                                    var tid = child.Attribute("templateId")?.Value;
                                    var outp = child.Attribute("output")?.Value;
                                    if (!string.IsNullOrEmpty(tid)) bucket["osmanthus:pdfTemplate.templateId"] = tid;
                                    if (!string.IsNullOrEmpty(outp)) bucket["osmanthus:pdfTemplate.output"] = outp;
                                    break;
                                }
                        }
                    }
                    // ALFRESCO
                    else if (nsUri == alfrescoNs)
                    {
                        switch (local)
                        {
                            case "formKey":
                                if (child.Attribute("value")?.Value is { } fk && fk.Length > 0)
                                    bucket["alfresco:formKey"] = fk;
                                break;
                            case "scriptTask":
                                if (child.Attribute("script")?.Value is { } sc && sc.Length > 0)
                                    bucket["alfresco:scriptTask.script"] = sc;
                                break;
                        }
                    }
                    // MCP
                    else if (nsUri == mcpNs)
                    {
                        if (local == "mcpServiceTask")
                        {
                            foreach (var attr in child.Attributes())
                            {
                                if (attr.IsNamespaceDeclaration) continue;
                                if (!string.IsNullOrEmpty(attr.Value))
                                    bucket[$"mcp:mcpServiceTask.{attr.Name.LocalName}"] = attr.Value;
                            }
                        }
                    }
                    // GENERICS (nur wenn aktiviert)
                    else if (_options.NormalizeUnknownVendorExtensions && nsUri.Length > 0)
                    {
                        // Versuche Präfix zu ermitteln (falls nicht vorhanden -> generic)
                        var prefix = child.GetPrefixOfNamespace(child.Name.Namespace);
                        if (string.IsNullOrEmpty(prefix))
                        {
                            // Fallback Kürzel generieren (ns)
                            prefix = "ns";
                        }
                        // Sicherstellen dass nicht einer der bekannten Prefixe kollidiert ohne definierten Namespace
                        // (zero-break – ignorieren wenn zufällig gleich)
                        foreach (var attr in child.Attributes())
                        {
                            if (attr.IsNamespaceDeclaration) continue;
                            if (attr.Name.LocalName is "id" or "type" or "name") continue;
                            if (attr.Value.Length == 0) continue;
                            var key = $"{prefix}:{local}.{attr.Name.LocalName}";
                            if (!bucket.ContainsKey(key))
                                bucket[key] = attr.Value;
                        }
                        // Textinhalt optional
                        if (!child.HasElements && !child.HasAttributes && !string.IsNullOrWhiteSpace(child.Value))
                        {
                            var k = $"{prefix}:{local}.__text";
                            if (!bucket.ContainsKey(k))
                                bucket[k] = child.Value.Trim();
                        }
                    }
                }

                if (bucket.Count > 0)
                    tmp[ownerId] = bucket;
            }

            if (tmp.Count > 0)
            {
                vendorNormalized = tmp.ToDictionary(
                    e => e.Key,
                    e => (IReadOnlyDictionary<string, string>)new ReadOnlyDictionary<string, string>(e.Value),
                    StringComparer.Ordinal);
            }
        }
        return vendorNormalized;
    }

    public string Serialize(BpmnModel model)
    {
        if (_options.EnableNormalizedProjectionSerializer)
        {
            var normalizedSerializer = new NormalizedProjectionSerializer(_options);
            return normalizedSerializer.Serialize(model);
        }

        return new BpmnSerializer { RoundtripMode = _options.RoundtripMode }.Serialize(model);
    }

    private (LoopCharacteristics? loop, bool conflict) ParseLoopLocal(XElement sp, XNamespace ns, HashSet<string> pendingMiConflicts)
    {
        var res = ParseLoopWithConflict(sp, ns, pendingMiConflicts);
        if (res.conflict) pendingMiConflicts.Add(sp.Attribute("id")?.Value ?? "");
        return res;
    }

    private static (LoopCharacteristics? loop, bool conflict) ParseLoopWithConflict(XElement sp, XNamespace ns, HashSet<string> conflictSet)
    {
        var mi = sp.Element(ns + "multiInstanceLoopCharacteristics") ?? sp.Element("multiInstanceLoopCharacteristics");
        if (mi != null)
        {
            bool isSeq = mi.Attribute("isSequential")?.Value == "true";
            int? card = null; var cardText = mi.Element(ns + "loopCardinality")?.Value ?? mi.Element("loopCardinality")?.Value; if (int.TryParse(cardText, out var cParsed)) card = cParsed;
            var camundaCollection = mi.Attribute(XName.Get("collection", "http://camunda.org/schema/1.0/bpmn"))?.Value;
            var zeebeCollection = mi.Element(XName.Get("inputCollection", "http://zeebe.io/schema/zeebe/1.0"))?.Value;
            var collectionRaw = camundaCollection ?? zeebeCollection;
            var camundaElementVar = mi.Attribute(XName.Get("elementVariable", "http://camunda.org/schema/1.0/bpmn"))?.Value;
            var zeebeInputElement = mi.Element(XName.Get("inputElement", "http://zeebe.io/schema/zeebe/1.0"))?.Value;
            var zeebeOutputElement = mi.Element(XName.Get("outputElement", "http://zeebe.io/schema/zeebe/1.0"))?.Value;
            var elementVar = camundaElementVar ?? zeebeInputElement ?? zeebeOutputElement;
            var completion = mi.Element(ns + "completionCondition")?.Value ?? mi.Element("completionCondition")?.Value;
            bool conflict = !string.IsNullOrWhiteSpace(collectionRaw) && card.HasValue;
            if (!string.IsNullOrWhiteSpace(collectionRaw)) card = null;
            var loop = new MultiInstanceLoopCharacteristics(isSeq, card, collectionRaw, elementVar, completion, zeebeInputElement, zeebeOutputElement);
            return (loop, conflict);
        }
        var std = sp.Element(ns + "standardLoopCharacteristics") ?? sp.Element("standardLoopCharacteristics");
        if (std != null)
        {
            var loopCond = std.Element(ns + "loopCondition")?.Value ?? std.Element("loopCondition")?.Value;
            bool testBefore = std.Attribute("testBefore")?.Value == "true";
            int? loopMax = null; if (int.TryParse(std.Attribute("loopMaximum")?.Value, out var lm)) loopMax = lm;
            return (new StandardLoopCharacteristics(loopCond, testBefore, loopMax), false);
        }
        return (null, false);
    }

    private static IReadOnlyList<EventDefinition> ParseEventDefinitions(XElement evt, XNamespace ns)
    {
        // For backward compatibility, use basic options without validation
        var defaultOptions = new BpmnParserOptions { ValidateEventDefinitions = false };
        var (definitions, _) = ParseEventDefinitionsWithDiagnostics(evt, ns, defaultOptions);
        return definitions;
    }

    private static void MaybeThrowOnValidation(BpmnParserOptions options, IReadOnlyList<ValidationDiagnostic> diagnostics)
    {
        if (!options.ThrowOnFatalValidation) return;
        var threshold = options.MinimumThrowSeverity;
        // if any diagnostic meets threshold, throw with all diagnostics
        if (diagnostics.Any(d => d.Severity >= threshold))
        {
            // Create concise message summary
            var first = diagnostics.First(d => d.Severity >= threshold);
            throw new BpmnValidationException(
                $"BPMN validation failed (first: {first.Code} severity={first.Severity})",
                diagnostics);
        }
    }
    private static ReadOnlySpan<T> AsSpanSafe<T>(IReadOnlyList<T> collection)
    {
        return collection switch
        {
            List<T> list => CollectionsMarshal.AsSpan(list),
            T[] array => array.AsSpan(),
            _ => collection.ToArray().AsSpan() // Fallback - creates array once
        };
    }

    // Phase 7: Enhanced ParseEventDefinitions with vendor/unknown detection
    private static (IReadOnlyList<EventDefinition> definitions, IReadOnlyList<ValidationDiagnostic> diagnostics) ParseEventDefinitionsWithDiagnostics(XElement evt, XNamespace ns, BpmnParserOptions options)
    {
        var list = new List<EventDefinition>();
        var diagnostics = new List<ValidationDiagnostic>();
        var eventId = evt.Attribute("id")?.Value ?? "unknown";

        // Phase 7: Known standard BPMN event definition types
        var standardEventDefinitions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "timerEventDefinition", "messageEventDefinition", "signalEventDefinition",
            "errorEventDefinition", "conditionalEventDefinition", "terminateEventDefinition",
            "cancelEventDefinition", "compensateEventDefinition", "escalationEventDefinition",
            "linkEventDefinition"
        };

        foreach (var defElem in evt.Elements())
        {
            var localName = defElem.Name.LocalName;
            var isInStandardNamespace = defElem.Name.Namespace == ns;

            // Check if this is a standard BPMN event definition
            if (localName.EndsWith("EventDefinition", StringComparison.OrdinalIgnoreCase))
            {
                if (isInStandardNamespace && standardEventDefinitions.Contains(localName))
                {
                    // Parse standard event definitions
                    var eventDef = ParseStandardEventDefinition(defElem, ns, localName);
                    if (eventDef != null)
                    {
                        list.Add(eventDef);
                    }
                }
                else if (!isInStandardNamespace)
                {
                    // Phase 7: Unknown/vendor event definition detected
                    if (options.ValidateEventDefinitions && options.EnableAdvancedValidation)
                    {
                        // Get the namespace prefix for the event definition element
                        var prefix = defElem.GetPrefixOfNamespace(defElem.Name.Namespace);
                        var elementDisplayName = string.IsNullOrEmpty(prefix)
                            ? localName
                            : $"{prefix}:{localName}";

                        diagnostics.Add(new ValidationDiagnostic(
                            Code: "VEN-UNKNOWN-EVENT-DEFINITION",
                            Severity: ValidationSeverity.Info,
                            Message: $"Event '{eventId}' contains unknown event definition '{elementDisplayName}' (preserved in raw form)",
                            ElementId: eventId,
                            Category: "Vendor"));
                    }
                }
            }
            else if (!isInStandardNamespace && defElem.Name.Namespace != XNamespace.None)
            {
                // Phase 7: Vendor-specific element that doesn't follow standard naming pattern
                if (options.ValidateEventDefinitions && options.EnableAdvancedValidation)
                {
                    // Get the namespace prefix for the vendor element
                    var prefix = defElem.GetPrefixOfNamespace(defElem.Name.Namespace);
                    var elementDisplayName = string.IsNullOrEmpty(prefix)
                        ? localName
                        : $"{prefix}:{localName}";

                    diagnostics.Add(new ValidationDiagnostic(
                        Code: "VEN-UNKNOWN-EVENT-DEFINITION",
                        Severity: ValidationSeverity.Info,
                        Message: $"Event '{eventId}' contains unknown event definition '{elementDisplayName}' (preserved in raw form)",
                        ElementId: eventId,
                        Category: "Vendor"));
                }
            }
        }

        return (list, diagnostics);
    }
    // Phase 7: Helper method to parse standard event definitions
    private static EventDefinition? ParseStandardEventDefinition(XElement defElem, XNamespace ns, string localName)
    {
        return localName switch
        {
            "timerEventDefinition" => new TimerEventDefinition(
                defElem.Element(ns + "timeDate")?.Value ?? defElem.Element("timeDate")?.Value,
                defElem.Element(ns + "timeDuration")?.Value ?? defElem.Element("timeDuration")?.Value,
                defElem.Element(ns + "timeCycle")?.Value ?? defElem.Element("timeCycle")?.Value),

            "messageEventDefinition" => new MessageEventDefinition(
                defElem.Attribute("messageRef")?.Value ?? string.Empty,
                defElem.Attribute("correlationKey")?.Value),

            "signalEventDefinition" => new SignalEventDefinition(
                defElem.Attribute("signalRef")?.Value ?? string.Empty),

            "errorEventDefinition" => new ErrorEventDefinition(
                defElem.Attribute("errorRef")?.Value ?? string.Empty),

            "conditionalEventDefinition" => new ConditionalEventDefinition(
                defElem.Element(ns + "condition")?.Value ??
                defElem.Element("condition")?.Value ?? string.Empty),

            "terminateEventDefinition" => new TerminateEventDefinition(),

            "cancelEventDefinition" => new CancelEventDefinition(),

            "compensateEventDefinition" => new CompensationEventDefinition(
                defElem.Attribute("activityRef")?.Value),

            "escalationEventDefinition" => new EscalationEventDefinition(
                defElem.Attribute("escalationRef")?.Value ?? string.Empty),

            "linkEventDefinition" => new LinkEventDefinition(
                defElem.Attribute("name")?.Value ?? string.Empty),

            _ => null
        };
    }

    //Add structural model hashing for cache invalidation
    public string ComputeStructuralModelHash(BpmnModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var structuralContent = new StringBuilder();

        //Build deterministic representation of structural content
        AppendStructuralContent(structuralContent, model);

        // Compute SHA256 hash of structural content
        var contentBytes = Encoding.UTF8.GetBytes(structuralContent.ToString());
        var hashBytes = SHA256.HashData(contentBytes);

        return Convert.ToHexString(hashBytes);
    }

    private static void AppendStructuralContent(StringBuilder sb, BpmnModel model)
    {
        // Process ID
        sb.Append($"PROCESS:{model.ProcessId}|");

        // Events (sorted by id for deterministic output)
        sb.Append("EVENTS:");
        foreach (var evt in model.Events.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            sb.Append($"{evt.Id}:{evt.Type}");
            if (!string.IsNullOrEmpty(evt.Name))
                sb.Append($"#{evt.Name}");

            // Event definitions (sorted for consistency)
            if (evt.Definitions.Count > 0)
            {
                sb.Append("[");
                foreach (var def in evt.Definitions.OrderBy(d => d.GetType().Name))
                {
                    sb.Append($"{def.GetType().Name}:");
                    AppendEventDefinitionStructure(sb, def);
                    sb.Append(",");
                }
                sb.Append("]");
            }
            sb.Append("|");
        }

        // Tasks (sorted by id for deterministic output)
        sb.Append("TASKS:");
        foreach (var task in model.Tasks.OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            sb.Append($"{task.Id}:{task.Type}");
            if (!string.IsNullOrEmpty(task.Name))
                sb.Append($"#{task.Name}");
            sb.Append("|");
        }

        // Gateways (sorted by id for deterministic output)
        sb.Append("GATEWAYS:");
        foreach (var gateway in model.Gateways.OrderBy(g => g.Id, StringComparer.Ordinal))
        {
            sb.Append($"{gateway.Id}:{gateway.Type}");
            if (!string.IsNullOrEmpty(gateway.DefaultFlowId))
                sb.Append($"@{gateway.DefaultFlowId}");
            sb.Append("|");
        }

        // Subprocesses (sorted by id for deterministic output)
        sb.Append("SUBPROCESSES:");
        foreach (var subprocess in model.Subprocesses.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            sb.Append($"{subprocess.Id}:SP");
            if (subprocess.IsEventSubprocess)
                sb.Append(":EVT");
            if (subprocess.IsTransaction)
                sb.Append(":TX");
            sb.Append("|");
        }

        // Sequence flows (sorted by id for deterministic output)
        sb.Append("FLOWS:");
        foreach (var flow in model.SequenceFlows.OrderBy(f => f.Id, StringComparer.Ordinal))
        {
            sb.Append($"{flow.Id}:{flow.SourceRef}->{flow.TargetRef}");
            if (flow.IsDefault)
                sb.Append(":DEF");
            if (!string.IsNullOrEmpty(flow.ConditionExpression))
                sb.Append($":COND#{flow.ConditionExpression.GetHashCode():X}"); // Hash condition to avoid huge strings
            if (flow.Priority.HasValue)
                sb.Append($":PRI{flow.Priority.Value}");
            sb.Append("|");
        }

        // Global elements
        if (model.Messages.Count > 0)
        {
            sb.Append("MESSAGES:");
            foreach (var msg in model.Messages.OrderBy(m => m.Id, StringComparer.Ordinal))
            {
                sb.Append($"{msg.Id}");
                if (!string.IsNullOrEmpty(msg.Name))
                    sb.Append($"#{msg.Name}");
                sb.Append("|");
            }
        }

        if (model.Signals.Count > 0)
        {
            sb.Append("SIGNALS:");
            foreach (var sig in model.Signals.OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                sb.Append($"{sig.Id}");
                if (!string.IsNullOrEmpty(sig.Name))
                    sb.Append($"#{sig.Name}");
                sb.Append("|");
            }
        }

        if (model.Errors.Count > 0)
        {
            sb.Append("ERRORS:");
            foreach (var err in model.Errors.OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                sb.Append($"{err.Id}");
                if (!string.IsNullOrEmpty(err.Name))
                    sb.Append($"#{err.Name}");
                if (!string.IsNullOrEmpty(err.ErrorCode))
                    sb.Append($":{err.ErrorCode}");
                sb.Append("|");
            }
        }

        if (model.Escalations.Count > 0)
        {
            sb.Append("ESCALATIONS:");
            foreach (var esc in model.Escalations.OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                sb.Append($"{esc.Id}");
                if (!string.IsNullOrEmpty(esc.Name))
                    sb.Append($"#{esc.Name}");
                if (!string.IsNullOrEmpty(esc.EscalationCode))
                    sb.Append($":{esc.EscalationCode}");
                sb.Append("|");
            }
        }
    }

    private static void AppendEventDefinitionStructure(StringBuilder sb, EventDefinition def)
    {
        switch (def)
        {
            case TimerEventDefinition timer:
                if (!string.IsNullOrEmpty(timer.TimeDate))
                    sb.Append($"TD:{timer.TimeDate.GetHashCode():X}");
                if (!string.IsNullOrEmpty(timer.TimeDuration))
                    sb.Append($"DUR:{timer.TimeDuration.GetHashCode():X}");
                if (!string.IsNullOrEmpty(timer.TimeCycle))
                    sb.Append($"CYC:{timer.TimeCycle.GetHashCode():X}");
                break;

            case MessageEventDefinition message:
                if (!string.IsNullOrEmpty(message.MessageRef))
                    sb.Append($"REF:{message.MessageRef}");
                if (!string.IsNullOrEmpty(message.CorrelationKey))
                    sb.Append($"CORR:{message.CorrelationKey}");
                break;

            case SignalEventDefinition signal:
                if (!string.IsNullOrEmpty(signal.SignalRef))
                    sb.Append($"REF:{signal.SignalRef}");
                break;

            case ErrorEventDefinition error:
                if (!string.IsNullOrEmpty(error.ErrorRef))
                    sb.Append($"REF:{error.ErrorRef}");
                break;

            case ConditionalEventDefinition conditional:
                if (!string.IsNullOrEmpty(conditional.Condition))
                    sb.Append($"COND:{conditional.Condition.GetHashCode():X}");
                break;

            case EscalationEventDefinition escalation:
                if (!string.IsNullOrEmpty(escalation.EscalationRef))
                    sb.Append($"REF:{escalation.EscalationRef}");
                break;

            case LinkEventDefinition link:
                if (!string.IsNullOrEmpty(link.Name))
                    sb.Append($"NAME:{link.Name}");
                break;

            case CompensationEventDefinition compensation:
                if (!string.IsNullOrEmpty(compensation.ActivityRef))
                    sb.Append($"ACT:{compensation.ActivityRef}");
                break;

            // Terminal event definitions (no additional data)
            case TerminateEventDefinition:
            case CancelEventDefinition:
                break;
        }
    }

    private BpmnModel ApplyPostProcessing(BpmnModel model)
    {
        var processedModel = model;

        // Apply vendor extension handlers
        if (_options.VendorExtensionHandlers.Count > 0)
        {
            processedModel = ApplyVendorExtensionHandlers(processedModel);
        }

        // Apply redaction policies
        if (_options.RedactionPolicies != null)
        {
            var redactionProcessor = new BpmnRedactionProcessor(_options.RedactionPolicies);
            processedModel = redactionProcessor.ApplyRedaction(processedModel);
        }

        return processedModel;
    }

    private BpmnModel ApplyVendorExtensionHandlers(BpmnModel model)
    {
        var updatedTasks = ProcessTaskWithVendorHandlers(model);
        return model with { Tasks = updatedTasks };
    }

    private List<BpmnTask> ProcessTaskWithVendorHandlers(BpmnModel model)
    {
        // Process vendor extensions through registered handlers
        var updatedTasks = new List<BpmnTask>();
        var rawExtensions = model.RawMetadata?.RawExtensionElements;

        foreach (var task in model.Tasks)
        {
            // Access raw extension elements from the model's raw metadata

            if (rawExtensions?.Count > 0)
            {
                var additionalAttributes = new Dictionary<string, string>();

                foreach (var handler in _options.VendorExtensionHandlers)
                {
                    foreach (var extensionElement in rawExtensions.Values)
                    {
                        foreach (var childElement in extensionElement.Elements())
                        {
                            if (handler.CanHandle(childElement.Name.NamespaceName, childElement.Name.LocalName))
                            {
                                var result = handler.ProcessExtension(childElement, task.Id);
                                foreach (var attr in result.NormalizedAttributes)
                                {
                                    additionalAttributes[attr.Key] = attr.Value;
                                }
                            }
                        }
                    }
                }

                if (additionalAttributes.Count > 0)
                {
                    var combinedAttributes = new Dictionary<string, string>();
                    if (task.Attributes != null)
                    {
                        foreach (var attr in task.Attributes)
                            combinedAttributes[attr.Key] = attr.Value;
                    }
                    foreach (var attr in additionalAttributes)
                        combinedAttributes[attr.Key] = attr.Value;

                    updatedTasks.Add(task with { Attributes = (Dictionary<string, string>)combinedAttributes });
                }
            }

            updatedTasks.Add(task);
        }

        return updatedTasks;
    }

    private string Intern(string s)
    {
        if (!_options.InternIds) return s;
        if (string.IsNullOrEmpty(s)) return s;

        // Use SharedStringAtomTable for cross-parser memory efficiency
        return SharedStringAtomTable.Intern(s);
    }


}