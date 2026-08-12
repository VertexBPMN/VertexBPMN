using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Runtime;
using VertexBPMN.Engine.Ecosystem;
using VertexBPMN.Engine.Performance;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Engine.Parsing;

public partial class RefactoredBpmnParser : IBpmnParser
{
    private readonly BpmnParserOptions _options;
    private readonly Dictionary<string, LinkedListNode<(string Key, BpmnModel Model)>> _cacheIndex = new();
    private readonly LinkedList<(string Key, BpmnModel Model)> _lru = new();
    private readonly object _cacheLock = new();

    //Observability Infrastructure
    private readonly ActivitySource? _activitySource;
    private readonly ILogger<BpmnParser> _logger;
    private static readonly ActivitySource DefaultActivitySource = new("VertexBPMN.Parsing");

    private string Intern(string s)
    {
        if (!_options.InternIds) return s;
        if (string.IsNullOrEmpty(s)) return s;

        // Use SharedStringAtomTable for cross-parser memory efficiency
        return SharedStringAtomTable.Intern(s);
    }

    private readonly Tracer _tracer;
    private readonly Dictionary<string, XDocument> _documentCache = new();

    public RefactoredBpmnParser() : this(new BpmnParserOptions(),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<BpmnParser>.Instance, TracerProvider.Default)
    {
    }

    public RefactoredBpmnParser(BpmnParserOptions options) : this(options,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<BpmnParser>.Instance, TracerProvider.Default)
    {
    }

    public RefactoredBpmnParser(BpmnParserOptions options, ILogger<BpmnParser> logger) : this(options, logger,
        TracerProvider.Default)
    {
    }

    public RefactoredBpmnParser(BpmnParserOptions options, ILogger<BpmnParser> logger, TracerProvider tracerProvider)
    {
        _options = options;

        //Initialize observability components (zero allocation when disabled)
        _activitySource = _options.EnableTracing ? (_options.TracingActivitySource ?? DefaultActivitySource) : null;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tracer = tracerProvider.GetTracer("VertexBPMN");
    }

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
                var last = _lru.Last!;
                _lru.RemoveLast();
                _cacheIndex.Remove(last.Value.Key);
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
        if (_options.EnableStreamingParse && xml.Length > _options.StreamingThreshold)
        {
            var streamingParser = new BpmnStreamingParser(_options);
            var streamingModel = await streamingParser.ParseAsync(xml, cancellationToken);
            return ApplyPostProcessing(streamingModel);
        }

        var model = await Parse(xml, cancellationToken);

        return ApplyPostProcessing(model);
    }

    private Task<BpmnModel> Parse(string xml, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource?.StartActivity("BpmnParser.ParseAsync");

        if (TryGetCached(xml) is { } cached)
        {
            if (_options.EnableLogging)
                _logger.LogDebug("ParseAsync cache hit for XML hash {XmlHash}", Hash(xml)[..8]);
            return Task.FromResult(cached);
        }

        if (_options.EnableLogging)
        {
            _logger.LogDebug(
                "ParseStart: RoundtripMode={RoundtripMode}, BuildRuntimeProjection={BuildRuntimeProjection}, NormalizeVendorExtensions={NormalizeVendorExtensions}",
                _options.RoundtripMode, _options.BuildRuntimeProjection, _options.NormalizeVendorExtensions);
        }

        var strict = _options.RoundtripMode == BpmnRoundtripMode.Strict;
        var (doc, root, ns) = LoadDocument(xml);
        var process = doc.Descendants(ns + "process").FirstOrDefault();

        // Early exit: no process
        if (process == null)
        {
            var empty = CreateEmptyModelOnNoProcess(strict);
            FinalizeValidationAndTracing(activity, empty);
            Cache(xml, empty);
            return Task.FromResult(empty);
        }

        // Strict captures bound to definitions/process for raw metadata
        var raw = InitializeStrictRootCaptures(strict, root, process, ns);

        // Optional large-model optimizations for raw collections
        PrepareOptionalRawCaptureCollections(strict, process);

        // Global elements
        var messages = doc.Descendants(ns + "message").ToList();
        var signals = doc.Descendants(ns + "signal").ToList();
        var errors = doc.Descendants(ns + "error").ToList();
        var escalations = doc.Descendants(ns + "escalation").ToList();
        if (strict && raw.RawGlobalElements is not null)
            foreach (var g in messages.Concat(signals).Concat(errors).Concat(escalations))
                raw.RawGlobalElements.Add(new XElement(g));

        var messageModels = ParseMessages(messages);
        var signalModels = ParseSignals(signals);
        var errorModels = ParseErrors(errors);
        var escalationModels = ParseEscalations(escalations);

        // Defaults for gateways (root-level, as in original behavior)
        var defaultIds = ComputeGatewayDefaults(process);

        // Core walk: parse all process elements into a result bucket
        var walk = ParseProcessElements(process, ns, strict, defaultIds, cancellationToken);

        // Reference resolution diagnostics (messages/signals/errors/escalations)
        var diagnostics = new List<string>();
        AddReferenceDiagnostics(walk.Events, messageModels, signalModels, errorModels, escalationModels, diagnostics);

        // Multi-instance conflict diagnostics (from capture)
        foreach (var cid in walk.PendingMiConflicts)
            if (!string.IsNullOrEmpty(cid))
                diagnostics.Add($"multi-instance conflict on {cid}");

        // Default flow with condition rule (preserved twice as in original for zero-break)
        AddDefaultFlowConditionDiagnostics(walk.Flows, walk.RawCond, diagnostics);
        AddDefaultFlowConditionDiagnostics(walk.Flows, walk.RawCond, diagnostics);

        // Transaction scope rules for cancel/terminate end events
        AddCancelAndTerminateDiagnostics(walk.Events, walk.Subprocesses, walk.RawEvDefs, diagnostics);

        // Gateways outgoing rule
        foreach (var gw in walk.Gateways)
            if (!walk.Flows.Any(f => f.SourceRef == gw.Id))
                diagnostics.Add($"Gateway {gw.Id} has no outgoing");

        // Boundary attachedToRef existence
        foreach (var (bid, attached) in walk.BoundaryEvents)
            if (!string.IsNullOrEmpty(attached) && !walk.FlowNodeIds.Contains(attached!))
                diagnostics.Add($"boundaryEvent {bid} attachedToRef {attached} missing");

        // Boundary compensation cancelActivity=false rule
        AddBoundaryCompensationCancelActivityDiagnostics(walk.Events, walk.ElementsMetadata, diagnostics);

        // Link event diagnostics
        foreach (var kv in walk.LinkThrowCounts)
            if (kv.Value > 1)
                diagnostics.Add($"Multiple throw link events for {kv.Key}");
        foreach (var name in walk.LinkThrowCounts.Keys)
            if (!walk.LinkCatchNames.Contains(name))
                diagnostics.Add($"Unmatched link {name}");

        // Strict structural checks
        if (_options.StrictValidation)
        {
            foreach (var f in walk.Flows)
                if (!walk.FlowNodeIds.Contains(f.SourceRef) || !walk.FlowNodeIds.Contains(f.TargetRef))
                    diagnostics.Add($"SequenceFlow {f.Id} has invalid endpoints {f.SourceRef}->{f.TargetRef}");
            if (!walk.Events.Any(e => e.Type == "startEvent"))
                diagnostics.Add("No startEvent found in process");
        }

        // Diagram Interchange (shapes/edges + optional raw DI root)
        var (shapes, edges, diRoot) = ParseDiagramInterchange(doc, strict);

        // Subprocess child composition
        var subprocesses =
            ComposeSubprocessChildren(walk.Subprocesses, walk.Events, walk.Tasks, walk.Gateways, walk.Flows);

        // Collaboration (participants/message flows)
        var (participants, messageFlows) = _options.EnableCollaborationParsing
            ? ParseCollaboration(doc, ns)
            : (new List<BpmnParticipant>(), new List<BpmnMessageFlow>());

        // Global element kinds for raw metadata (strict only)
        IReadOnlyDictionary<string, string>? globalKinds = null;
        if (strict && _options.BuildGlobalElementIndex && raw.RawGlobalElements is {Count: > 0})
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var x in raw.RawGlobalElements)
            {
                var idAttr = x.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(idAttr)) continue;
                dict[idAttr] = x.Name.LocalName; // message | signal | error | escalation
            }

            globalKinds = dict;
        }

        // Vendor normalization + merge potentialOwner extras
        var vendorNormalized = ParseVendorExtensions(strict, walk.RawExtensions);
        vendorNormalized = MergePotentialOwnerExtras(vendorNormalized, walk.PotentialOwnerExtras);

        // Activities projection list (tasks + subprocesses)
        var activities = walk.Tasks.Cast<object>().Concat(subprocesses).ToList();

        // Raw metadata object (strict)
        var rawMeta = strict
            ? BuildRawMetadata(raw, walk, diRoot, globalKinds, vendorNormalized)
            : null;

        // Runtime projection (optional)
        var pid = Intern(process.Attribute("id")?.Value ?? string.Empty);
        var pname = Intern(process.Attribute("id")?.Value ?? string.Empty);
        var runtime = _options.BuildRuntimeProjection
            ? RuntimeProjectionBuilder.Build(_options, pid,
                walk.Events, walk.Tasks, walk.Gateways, subprocesses, walk.Flows,
                vendorNormalized, rawMeta, walk.ScriptTaskRaw, walk.PotentialOwnerExtras)
            : null;

        // Build final model
        var model = new BpmnModel(
            pid,
            pname,
            walk.Events,
            walk.Gateways,
            subprocesses,
            walk.Flows,
            walk.Tasks,
            walk.DataObjects,
            walk.DataObjectRefs,
            walk.DataStores,
            walk.DataStoreRefs,
            walk.Properties,
            walk.ActivityIo,
            messageModels,
            signalModels,
            errorModels,
            escalationModels,
            diagnostics,
            shapes,
            edges,
            participants,
            walk.Lanes,
            messageFlows,
            walk.TextAnnotations,
            walk.Associations,
            walk.Groups,
            Activities: activities,
            RawMetadata: rawMeta
        )
        {
            Runtime = runtime
        };

        // Structured validation + merge unknown event definition diagnostics
        IReadOnlyList<ValidationDiagnostic>? structuredDiagnostics = null;
        if (_options.EnableAdvancedValidation)
        {
            structuredDiagnostics = ValidateModel(model, _options);
            if (walk.UnknownEventDefinitionDiagnostics.Count > 0)
            {
                var allDiagnostics = new List<ValidationDiagnostic>(structuredDiagnostics);
                allDiagnostics.AddRange(walk.UnknownEventDefinitionDiagnostics);
                structuredDiagnostics = allDiagnostics;
            }

            if (_options.EnableLogging && structuredDiagnostics.Count > 0)
            {
                var errorCount = structuredDiagnostics.Count(d => d.Severity >= ValidationSeverity.Error);
                var warningCount = structuredDiagnostics.Count(d => d.Severity == ValidationSeverity.Warning);
                _logger.LogInformation(
                    "ValidationSummary: ProcessId={ProcessId}, Errors={ErrorCount}, Warnings={WarningCount}, TotalDiagnostics={TotalCount}",
                    pid, errorCount, warningCount, structuredDiagnostics.Count);
            }
        }

        if (_options.EnableAdvancedValidation &&
            _options.ThrowOnFatalValidation &&
            structuredDiagnostics is {Count: > 0})
        {
            MaybeThrowOnValidation(_options, structuredDiagnostics);
        }

        model.ValidationDiagnostics = structuredDiagnostics;

        // Tracing tags
        if (activity != null)
        {
            activity.SetTag("bpmn.process_id", pid);
            activity.SetTag("bpmn.node_count",
                (walk.Events.Count + walk.Tasks.Count + walk.Gateways.Count + subprocesses.Count).ToString());
            activity.SetTag("bpmn.flow_count", walk.Flows.Count.ToString());
            activity.SetTag("bpmn.roundtrip_mode", _options.RoundtripMode.ToString());
            activity.SetTag("bpmn.runtime_projection", _options.BuildRuntimeProjection.ToString().ToLowerInvariant());
            activity.SetTag("bpmn.vendor_normalization",
                _options.NormalizeVendorExtensions.ToString().ToLowerInvariant());

            if (structuredDiagnostics != null)
            {
                activity.SetTag("bpmn.validation_errors",
                    structuredDiagnostics.Count(d => d.Severity >= ValidationSeverity.Error).ToString());
                activity.SetTag("bpmn.validation_warnings",
                    structuredDiagnostics.Count(d => d.Severity == ValidationSeverity.Warning).ToString());
            }
            else
            {
                activity.SetTag("bpmn.validation_errors", "0");
                activity.SetTag("bpmn.validation_warnings", "0");
            }
        }

        if (_options.EnableLogging)
        {
            _logger.LogDebug(
                "PhaseComplete: ProcessId={ProcessId}, ParsedSuccessfully=true, DiagnosticsCount={DiagnosticsCount}",
                pid, diagnostics.Count);
        }

        Cache(xml, model);
        return Task.FromResult(model);
    }


    private (XDocument doc, XElement root, XNamespace ns) LoadDocument(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var root = doc.Root!;
        var ns = root.Name.Namespace;
        return (doc, root, ns);
    }

    private BpmnModel CreateEmptyModelOnNoProcess(bool strict)
    {
        var rawMeta = strict
            ? new BpmnRawMetadata(new Dictionary<string, string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal))
            : null;

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
            new List<string> {"No <process> element"},
            RawMetadata: rawMeta
        );
        empty.Runtime = null;

        IReadOnlyList<ValidationDiagnostic>? structured = null;
        if (strict && _options.EnableAdvancedValidation)
            structured = ValidateModel(empty, _options);

        if (_options.EnableAdvancedValidation &&
            _options.ThrowOnFatalValidation &&
            structured is {Count: > 0})
        {
            MaybeThrowOnValidation(_options, structured);
        }

        empty.ValidationDiagnostics = structured;
        return empty;
    }

    private (Dictionary<string, string>? RawDefinitionsAttr,
        Dictionary<string, string>? RawProcessAttr,
        List<NamespacePrefix>? NamespacePrefixes,
        List<XElement>? RawGlobalElements) InitializeStrictRootCaptures(bool strict, XElement root, XElement process,
            XNamespace ns)
    {
        Dictionary<string, string>? rawDefinitionsAttr = strict ? new(StringComparer.Ordinal) : null;
        Dictionary<string, string>? rawProcessAttr = strict ? new(StringComparer.Ordinal) : null;
        List<NamespacePrefix>? namespacePrefixes = strict ? new() : null;
        List<XElement>? rawGlobalElements = strict ? new() : null;

        if (strict)
        {
            foreach (var attr in root.Attributes())
            {
                if (attr.IsNamespaceDeclaration)
                {
                    var prefix = (attr.Name.Namespace == XNamespace.None && attr.Name.LocalName == "xmlns")
                        ? string.Empty
                        : attr.Name.LocalName;
                    namespacePrefixes!.Add(new NamespacePrefix(prefix, attr.Value, true));
                }
            }

            foreach (var a in root.Attributes())
            {
                if (!a.IsNamespaceDeclaration) rawDefinitionsAttr![a.Name.ToString()] = a.Value;
            }

            foreach (var a in process.Attributes()) rawProcessAttr![a.Name.ToString()] = a.Value;
        }

        // Process-level documentation capture stays here (strict only, respecting large model opts)
        if (strict &&
            !(_options.SkipDocumentationForLargeModels && IsLargeModel(process, _options)))
        {
            var rawDocumentation = new Dictionary<string, List<XElement>>();
            var docNodes = process.Elements(ns + "documentation").Concat(process.Elements("documentation"));
            foreach (var dn in docNodes)
            {
                if (!rawDocumentation.TryGetValue("__process", out var list))
                {
                    list = new();
                    rawDocumentation["__process"] = list;
                }

                list.Add(new XElement(dn));
            }
        }

        return (rawDefinitionsAttr, rawProcessAttr, namespacePrefixes, rawGlobalElements);
    }

    private void PrepareOptionalRawCaptureCollections(bool strict, XElement process)
    {
        var large = IsLargeModel(process, _options);
        // No-op here: the actual raw collections are allocated inside ParseProcessElements only when needed,
        // with the same large-model checks as in your original implementation.
    }

    // Same behavior: compute defaultIds from root-level gateways only
    private HashSet<string> ComputeGatewayDefaults(XElement process)
    {
        var gatewaysRaw = process.Elements()
            .Where(e => e.Name.LocalName.EndsWith("Gateway"))
            .Select(g => new
            {
                Id = Intern(g.Attribute("id")?.Value ?? string.Empty),
                Type = g.Name.LocalName,
                DefaultId = g.Attribute("default")?.Value
            })
            .ToList();
        return new HashSet<string>(gatewaysRaw.Select(g => g.DefaultId).Where(v => !string.IsNullOrWhiteSpace(v))!,
            StringComparer.Ordinal);
    }

    private ProcessParseResult ParseProcessElements(XElement process, XNamespace ns, bool strict,
        HashSet<string> defaultIds, CancellationToken cancellationToken)
    {
        var res = new ProcessParseResult();

        // Allocate raw captures according to strict + large-model options
        res.RawDocumentation =
            (strict && !(IsLargeModel(process, _options) && _options.SkipDocumentationForLargeModels))
                ? new Dictionary<string, List<XElement>>()
                : null;
        res.RawArtifacts = (strict && !(IsLargeModel(process, _options) && _options.SkipArtifactsForLargeModels))
            ? new List<XElement>()
            : null;
        res.RawExtensions = (strict && !(IsLargeModel(process, _options) && _options.SkipExtensionsForLargeModels))
            ? new Dictionary<string, XElement>()
            : null;

        res.RawEvDefs = strict && _options.CaptureRawEventDefinitions ? new Dictionary<string, List<XElement>>() : null;
        res.RawMultiInstance = strict ? new Dictionary<string, XElement>() : null;
        res.PriorityAttrNs = strict ? new Dictionary<string, string>(StringComparer.Ordinal) : null;
        res.FlowNodeAttributes =
            strict ? new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal) : null;
        res.ElementsMetadata = strict ? new Dictionary<string, ElementMetadata>() : null;
        res.RawLanes = strict ? new List<XElement>() : null;

        var subprocessStack = new Stack<string>();
        var orderCounter = 0;

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
                {
                    if (!res.IdIndex.Add(id) && _options.StrictValidation)
                        // Note: legacy diagnostics are added later via ValidateModel conversion; keep raw text
                        ; // We keep duplicate detection but do not add text here to avoid double reporting.
                }

                var ext = ExtractExtensions(el, ns, strict, res.RawExtensions);

                switch (local)
                {
                    case "subProcess":
                    case "adHocSubProcess":
                    case "transaction":
                    {
                        var isEvent = el.Attribute("triggeredByEvent")?.Value == "true";
                        var isTx = local == "transaction" ||
                                   el.Attribute("transaction")?.Value == "true";

                        var loopInfo = ParseLoopLocal(el, ns, res.PendingMiConflicts);

                        if (strict && !string.IsNullOrEmpty(id))
                        {
                            var miNode = el.Element(ns + "multiInstanceLoopCharacteristics") ??
                                         el.Element("multiInstanceLoopCharacteristics");
                            var stdNode = el.Element(ns + "standardLoopCharacteristics") ??
                                          el.Element("standardLoopCharacteristics");
                            if (miNode != null || stdNode != null)
                                res.RawMultiInstance![id] = new XElement(miNode ?? stdNode);
                        }

                        res.Subprocesses.Add(new BpmnSubprocess(id, isEvent, isTx, loopInfo.loop, currentSub, ext));
                        if (isTx && !string.IsNullOrEmpty(id)) res.TransactionIds.Add(id);
                        res.FlowNodeIds.Add(id);

                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id,
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
                    }
                    case "startEvent":
                    case "endEvent":
                    case "intermediateCatchEvent":
                    case "intermediateThrowEvent":
                    case "boundaryEvent":
                    {
                        var (defs, eventDefDiagnostics) = ParseEventDefinitionsWithDiagnostics(el, ns, _options);
                        res.UnknownEventDefinitionDiagnostics.AddRange(eventDefDiagnostics);

                        res.Events.Add(new BpmnEvent(id, local, defs, currentSub, ext));
                        res.FlowNodeIds.Add(id);

                        if (local == "boundaryEvent")
                            res.BoundaryEvents.Add((id, el.Attribute("attachedToRef")?.Value));

                        // link events tracking
                        foreach (var led in el.Elements())
                            if (led.Name.LocalName == "linkEventDefinition")
                            {
                                var lname = led.Attribute("name")?.Value;
                                if (!string.IsNullOrEmpty(lname))
                                {
                                    if (local == "intermediateThrowEvent")
                                    {
                                        res.LinkThrowCounts.TryGetValue(lname!, out var c);
                                        res.LinkThrowCounts[lname!] = c + 1;
                                    }
                                    else if (local == "intermediateCatchEvent")
                                    {
                                        res.LinkCatchNames.Add(lname!);
                                    }
                                }
                            }

                        if (strict && _options.CaptureRawEventDefinitions)
                        {
                            var list = new List<XElement>();
                            foreach (var d in el.Elements())
                            {
                                if (d.Name.LocalName.EndsWith("EventDefinition", StringComparison.OrdinalIgnoreCase) ||
                                    d.Name.LocalName.Contains("EventDefinition", StringComparison.OrdinalIgnoreCase) ||
                                    (d.Name.Namespace != ns && d.Name.Namespace != XNamespace.None))
                                {
                                    list.Add(new XElement(d));
                                }
                            }

                            if (list.Count > 0) res.RawEvDefs![id] = list;
                        }

                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case var _ when local.EndsWith("Task") || local == "callActivity":
                    {
                        if (strict && !string.IsNullOrEmpty(id))
                        {
                            var miNodeT = el.Element(ns + "multiInstanceLoopCharacteristics") ??
                                          el.Element("multiInstanceLoopCharacteristics");
                            var stdNodeT = el.Element(ns + "standardLoopCharacteristics") ??
                                           el.Element("standardLoopCharacteristics");
                            if (miNodeT != null || stdNodeT != null)
                                res.RawMultiInstance![id] = new XElement(miNodeT ?? stdNodeT);
                        }

                        if (local == "scriptTask" && !string.IsNullOrEmpty(id))
                        {
                            var fmt = el.Attribute("scriptFormat")?.Value;
                            var body = el.Element(ns + "script")?.Value ?? el.Element("script")?.Value;
                            var resVar = el.Attribute("resultVariable")?.Value;
                            res.ScriptTaskRaw[id] = (fmt, body, resVar);
                        }

                        if (local == "userTask" && !string.IsNullOrEmpty(id))
                        {
                            IEnumerable<XElement> roles = el.Elements().Where(e =>
                                e.Name.LocalName == "potentialOwner" ||
                                (e.Name.LocalName == "resourceRole" &&
                                 (string?) e.Attribute(XName.Get("type",
                                     "http://www.w3.org/2001/XMLSchema-instance")) == "potentialOwner"));

                            foreach (var role in roles)
                            {
                                var formal = role.Element(ns + "resourceAssignmentExpression") ??
                                             role.Element("resourceAssignmentExpression");
                                var expr = formal?.Element(ns + "formalExpression") ??
                                           formal?.Element("formalExpression");
                                var text = expr?.Value?.Trim();
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    res.PotentialOwnerExtras[id] = text!;
                                }
                            }
                        }

                        var task = new BpmnTask(id, local, currentSub, ext)
                        {
                            Name = el.Attribute("name")?.Value ?? string.Empty
                        };
                        res.Tasks.Add(task);
                        res.FlowNodeIds.Add(id);

                        // IO spec & associations
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

                            var dataOutputs = ioSpec.Elements(ns + "dataOutput").Concat(ioSpec.Elements("dataOutput"))
                                .Select(dout =>
                                {
                                    var oid = Intern(dout.Attribute("id")?.Value ?? string.Empty);
                                    if (string.IsNullOrEmpty(oid)) return null;
                                    return new BpmnDataOutput(oid, dout.Attribute("name")?.Value);
                                }).OfType<BpmnDataOutput>().ToList();

                            var inputAssociations = el.Elements(ns + "dataInputAssociation")
                                .Concat(el.Elements("dataInputAssociation"))
                                .Select(a =>
                                {
                                    var src = a.Element(ns + "sourceRef")?.Value ?? a.Element("sourceRef")?.Value;
                                    var tgt = a.Element(ns + "targetRef")?.Value ?? a.Element("targetRef")?.Value;
                                    if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(tgt)) return null;
                                    return new BpmnDataAssociation(Intern(src), Intern(tgt));
                                }).OfType<BpmnDataAssociation>().ToList();

                            var outputAssociations = el.Elements(ns + "dataOutputAssociation")
                                .Concat(el.Elements("dataOutputAssociation"))
                                .Select(a =>
                                {
                                    var src = a.Element(ns + "sourceRef")?.Value ?? a.Element("sourceRef")?.Value;
                                    var tgt = a.Element(ns + "targetRef")?.Value ?? a.Element("targetRef")?.Value;
                                    if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(tgt)) return null;
                                    return new BpmnDataAssociation(Intern(src), Intern(tgt));
                                }).OfType<BpmnDataAssociation>().ToList();

                            if (dataInputs.Count > 0 || dataOutputs.Count > 0 ||
                                inputAssociations.Count > 0 || outputAssociations.Count > 0)
                            {
                                res.ActivityIo.Add(new BpmnActivityIo(id, dataInputs, dataOutputs, inputAssociations,
                                    outputAssociations));
                            }
                        }

                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case var _ when local.EndsWith("Gateway"):
                    {
                        res.FlowNodeIds.Add(id);
                        res.Gateways.Add(new BpmnGateway(id, local, /* default id resolved via precomputed set */ null,
                            currentSub, ext));
                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case "laneSet":
                    {
                        if (strict) res.RawLanes!.Add(new XElement(el));
                        Walk(el);
                        continue;
                    }

                    case "sequenceFlow":
                    {
                        int? priority = null;
                        var prAttr = el.Attribute(XName.Get("priority", "http://vertexbpmn.io/schema/1.0")) ??
                                     el.Attribute(XName.Get("priority", "http://camunda.org/schema/1.0/bpmn")) ??
                                     el.Attribute("priority");

                        if (prAttr != null && int.TryParse(prAttr.Value, out var pVal)) priority = pVal;
                        if (strict && prAttr != null && !string.IsNullOrEmpty(id))
                            res.PriorityAttrNs![id] = prAttr.Name.NamespaceName;

                        var condNode = el.Element(ns + "conditionExpression") ?? el.Element("conditionExpression");
                        var condText = condNode?.Value?.Trim();

                        res.Flows.Add(new BpmnSequenceFlow(
                            id,
                            Intern(el.Attribute("sourceRef")?.Value ?? string.Empty),
                            Intern(el.Attribute("targetRef")?.Value ?? string.Empty),
                            defaultIds.Contains(id),
                            condText,
                            currentSub,
                            ext,
                            priority));

                        if (strict && condNode != null)
                        {
                            bool wasCData = condNode.Nodes().OfType<XCData>().Any();
                            res.RawCond![id] = (condNode.Value, wasCData);
                        }

                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case "dataObject":
                    {
                        res.DataObjects.Add(new BpmnDataObject(id, el.Attribute("name")?.Value));
                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case "dataObjectReference":
                    {
                        res.DataObjectRefs.Add(new BpmnDataObjectReference(id,
                            Intern(el.Attribute("dataObjectRef")?.Value ?? string.Empty)));
                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case "dataStore":
                    {
                        res.DataStores.Add(new BpmnDataStore(id, el.Attribute("name")?.Value));
                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case "dataStoreReference":
                    {
                        res.DataStoreRefs.Add(new BpmnDataStoreReference(id,
                            Intern(el.Attribute("dataStoreRef")?.Value ?? string.Empty)));
                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case "property":
                    {
                        res.Properties.Add(new BpmnProperty(id, el.Attribute("name")?.Value));
                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case "textAnnotation":
                    {
                        if (!_options.CaptureArtifacts && strict)
                        {
                            CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation,
                                res.ElementsMetadata!,
                                orderCounter, ns, el, id);
                            break;
                        }

                        res.TextAnnotations.Add(new BpmnTextAnnotation(id,
                            el.Element(ns + "text")?.Value ?? el.Element("text")?.Value));
                        if (strict) res.RawArtifacts!.Add(new XElement(el));
                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case "group":
                    {
                        if (!_options.CaptureArtifacts && strict)
                        {
                            CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation,
                                res.ElementsMetadata!,
                                orderCounter, ns, el, id);
                            break;
                        }

                        res.Groups.Add(new BpmnGroup(id, el.Attribute("categoryValueRef")?.Value));
                        if (strict) res.RawArtifacts!.Add(new XElement(el));
                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case "association":
                    {
                        if (!_options.CaptureArtifacts && strict)
                        {
                            CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation,
                                res.ElementsMetadata!,
                                orderCounter, ns, el, id);
                            break;
                        }

                        res.Associations.Add(new BpmnAssociation(
                            id,
                            el.Attribute("sourceRef")?.Value ?? string.Empty,
                            el.Attribute("targetRef")?.Value ?? string.Empty,
                            el.Attribute("associationDirection")?.Value));
                        if (strict) res.RawArtifacts!.Add(new XElement(el));
                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }

                    case "lane":
                    {
                        if (strict) res.RawLanes!.Add(new XElement(el));
                        CaptureElementMeta(strict, res.FlowNodeAttributes, res.RawDocumentation, res.ElementsMetadata!,
                            orderCounter, ns, el, id);
                        break;
                    }
                }

                // missing id diagnostic (legacy – kept for ValidateModel conversion)
                if (strict && string.IsNullOrEmpty(id))
                {
                    if (local is "userTask" or "serviceTask" or "task" || local.EndsWith("Task") ||
                        local is "startEvent" or "endEvent" or "intermediateCatchEvent" or "intermediateThrowEvent"
                            or "boundaryEvent" ||
                        local.EndsWith("Gateway") || local is "sequenceFlow")
                    {
                        // legacy text; converted later in ValidateModel
                        // (do not add here to avoid duplication; ValidateModel derives STR-MISSING-ID anyway)
                    }
                }
            }
        }

        Walk(process);
        return res;
    }

    private void AddReferenceDiagnostics(
        List<BpmnEvent> events,
        List<BpmnMessage> messages,
        List<BpmnSignal> signals,
        List<BpmnError> errors,
        List<BpmnEscalation> escalations,
        List<string> diagnostics)
    {
        if (events.Count == 0) return;

        var messageIds = new HashSet<string>(messages.Select(m => m.Id), StringComparer.Ordinal);
        var signalIds = new HashSet<string>(signals.Select(s => s.Id), StringComparer.Ordinal);
        var errorIds = new HashSet<string>(errors.Select(e => e.Id), StringComparer.Ordinal);
        var escalationIds = new HashSet<string>(escalations.Select(e => e.Id), StringComparer.Ordinal);

        foreach (var ev in events)
        {
            foreach (var def in ev.Definitions)
            {
                switch (def)
                {
                    case MessageEventDefinition m
                        when !string.IsNullOrEmpty(m.MessageRef) && !messageIds.Contains(m.MessageRef):
                        diagnostics.Add($"Unknown messageRef '{m.MessageRef}' at event {ev.Id}");
                        break;
                    case SignalEventDefinition s
                        when !string.IsNullOrEmpty(s.SignalRef) && !signalIds.Contains(s.SignalRef):
                        diagnostics.Add($"Unknown signalRef '{s.SignalRef}' at event {ev.Id}");
                        break;
                    case ErrorEventDefinition e
                        when !string.IsNullOrEmpty(e.ErrorRef) && !errorIds.Contains(e.ErrorRef):
                        diagnostics.Add($"Unknown errorRef '{e.ErrorRef}' at event {ev.Id}");
                        break;
                    case EscalationEventDefinition esc when !string.IsNullOrEmpty(esc.EscalationRef) &&
                                                            !escalationIds.Contains(esc.EscalationRef):
                        diagnostics.Add($"Unknown escalationRef '{esc.EscalationRef}' at event {ev.Id}");
                        break;
                }
            }
        }
    }

    private void AddDefaultFlowConditionDiagnostics(
        List<BpmnSequenceFlow> flows,
        Dictionary<string, (string Raw, bool WasCData)>? rawCond,
        List<string> diagnostics)
    {
        foreach (var f in flows.Where(f => f.IsDefault))
        {
            bool hasCond = !string.IsNullOrWhiteSpace(f.ConditionExpression) ||
                           (rawCond != null && rawCond.TryGetValue(f.Id, out var rc) && !string.IsNullOrEmpty(rc.Raw));
            if (hasCond) diagnostics.Add($"Default flow {f.Id} has condition");
        }
    }

    private void AddCancelAndTerminateDiagnostics(
        List<BpmnEvent> events,
        List<BpmnSubprocess> subprocesses,
        Dictionary<string, List<XElement>>? rawEvDefs,
        List<string> diagnostics)
    {
        foreach (var ev in events.Where(e => e.Type == "endEvent"))
        {
            bool hasCancel = rawEvDefs?.ContainsKey(ev.Id) == true &&
                             rawEvDefs![ev.Id].Any(x => x.Name.LocalName == "cancelEventDefinition");
            if (!hasCancel) hasCancel = ev.Definitions.OfType<CancelEventDefinition>().Any();

            bool hasTerminate = ev.Definitions.OfType<TerminateEventDefinition>().Any();

            if (hasCancel || hasTerminate)
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

                if (hasCancel && !insideTx) diagnostics.Add($"Cancel end event {ev.Id} outside transaction");
                if (hasTerminate && !insideTx) diagnostics.Add($"Terminate end event {ev.Id} outside transaction");
            }
        }
    }

    private void AddBoundaryCompensationCancelActivityDiagnostics(
        List<BpmnEvent> events,
        Dictionary<string, ElementMetadata>? elementsMetadata,
        List<string> diagnostics)
    {
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
    }

    private (List<BpmnShape>? shapes, List<BpmnEdge>? edges, XElement? diRoot) ParseDiagramInterchange(XDocument doc,
        bool strict)
    {
        if (!_options.ParseDiagramInterchange)
            return (null, null, null);

        var bpmndi = (XNamespace) "http://www.omg.org/spec/BPMN/20100524/DI";
        var omgdc = (XNamespace) "http://www.omg.org/spec/DD/20100524/DC";
        var omgdi = (XNamespace) "http://www.omg.org/spec/DD/20100524/DI";

        var shapes = new List<BpmnShape>();
        var edges = new List<BpmnEdge>();

        foreach (var shape in doc.Descendants(bpmndi + "BPMNShape"))
        {
            var id = Intern(shape.Attribute("id")?.Value ?? string.Empty);
            var bpmnElement = Intern(shape.Attribute("bpmnElement")?.Value ?? string.Empty);
            var bounds = shape.Element(omgdc + "Bounds");
            if (bounds != null &&
                double.TryParse(bounds.Attribute("x")?.Value, out var x) &&
                double.TryParse(bounds.Attribute("y")?.Value, out var y) &&
                double.TryParse(bounds.Attribute("width")?.Value, out var w) &&
                double.TryParse(bounds.Attribute("height")?.Value, out var h))
            {
                shapes.Add(new BpmnShape(id, bpmnElement, x, y, w, h));
            }
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

        XElement? diRoot = null;
        if (strict && shapes.Count + edges.Count > 0 && _options.CaptureDiRaw)
            diRoot = doc.Descendants(bpmndi + "BPMNDiagram").FirstOrDefault()?.Parent;

        return (shapes, edges, diRoot);
    }

    private List<BpmnSubprocess> ComposeSubprocessChildren(
        List<BpmnSubprocess> subprocesses,
        List<BpmnEvent> events,
        List<BpmnTask> tasks,
        List<BpmnGateway> gateways,
        List<BpmnSequenceFlow> flows)
    {
        if (subprocesses.Count == 0) return subprocesses;

        var updated = new List<BpmnSubprocess>(subprocesses.Count);

        var subprocessesSpan = CollectionsMarshal.AsSpan(subprocesses);
        var eventsSpan = CollectionsMarshal.AsSpan(events);
        var tasksSpan = CollectionsMarshal.AsSpan(tasks);
        var gatewaysSpan = CollectionsMarshal.AsSpan(gateways);
        var flowsSpan = CollectionsMarshal.AsSpan(flows);

        foreach (var sp in subprocessesSpan)
        {
            var childFlowNodes = new List<string>();
            foreach (var e in eventsSpan)
                if (e.SubprocessId == sp.Id)
                    childFlowNodes.Add(e.Id);
            foreach (var t in tasksSpan)
                if (t.SubprocessId == sp.Id)
                    childFlowNodes.Add(t.Id);
            foreach (var g in gatewaysSpan)
                if (g.SubprocessId == sp.Id)
                    childFlowNodes.Add(g.Id);
            foreach (var s2 in subprocessesSpan)
                if (s2.SubprocessId == sp.Id)
                    childFlowNodes.Add(s2.Id);

            var childSeqFlows = new List<string>();
            foreach (var f in flowsSpan)
                if (f.SubprocessId == sp.Id)
                    childSeqFlows.Add(f.Id);

            updated.Add(sp with {ChildFlowNodeIds = childFlowNodes, ChildSequenceFlowIds = childSeqFlows});
        }

        return updated;
    }

    private (List<BpmnParticipant> participants, List<BpmnMessageFlow> messageFlows) ParseCollaboration(XDocument doc,
        XNamespace ns)
    {
        var participants = new List<BpmnParticipant>();
        var messageFlows = new List<BpmnMessageFlow>();

        var collab = doc.Descendants(ns + "collaboration").FirstOrDefault();
        if (collab != null)
        {
            foreach (var part in collab.Elements(ns + "participant"))
            {
                var pidAttr = part.Attribute("id")?.Value;
                var pnameAttr = part.Attribute("name")?.Value ?? string.Empty;
                var pref = part.Attribute("processRef")?.Value;
                if (!string.IsNullOrEmpty(pidAttr))
                    participants.Add(new BpmnParticipant(pidAttr, pnameAttr, pref ?? string.Empty));
            }

            foreach (var mf in collab.Elements(ns + "messageFlow"))
            {
                var id = mf.Attribute("id")?.Value;
                if (string.IsNullOrEmpty(id)) continue;

                var name = mf.Attribute("name")?.Value ?? string.Empty;
                var src = mf.Attribute("sourceRef")?.Value ?? string.Empty;
                var tgt = mf.Attribute("targetRef")?.Value ?? string.Empty;
                messageFlows.Add(new BpmnMessageFlow(id, src, tgt, name));
            }
        }

        return (participants, messageFlows);
    }

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? MergePotentialOwnerExtras(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? vendorNormalized,
        Dictionary<string, string> potentialOwnerExtras)
    {
        if (potentialOwnerExtras.Count == 0) return vendorNormalized;

        var merged = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        if (vendorNormalized != null)
            foreach (var kv in vendorNormalized)
                merged[kv.Key] = kv.Value;

        foreach (var kv in potentialOwnerExtras)
        {
            if (!merged.TryGetValue(kv.Key, out var existing))
            {
                merged[kv.Key] = new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(StringComparer.Ordinal) {["potentialOwner"] = kv.Value});
            }
            else
            {
                var dict = new Dictionary<string, string>(existing, StringComparer.Ordinal)
                {
                    ["potentialOwner"] = kv.Value
                };
                merged[kv.Key] = new ReadOnlyDictionary<string, string>(dict);
            }
        }

        return merged;
    }

    private BpmnRawMetadata BuildRawMetadata(
        (Dictionary<string, string>? RawDefinitionsAttr,
            Dictionary<string, string>? RawProcessAttr,
            List<NamespacePrefix>? NamespacePrefixes,
            List<XElement>? RawGlobalElements) rootRaw,
        ProcessParseResult walk,
        XElement? diRoot,
        IReadOnlyDictionary<string, string>? globalKinds,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? vendorNormalized)
    {
        // Optimize memory like original
        if (_options.OptimizeStrictMemory)
        {
            if (walk.RawCond is {Count: 0}) walk.RawCond = null;
            if (walk.RawExtensions is {Count: 0}) walk.RawExtensions = null;
            if (walk.RawEvDefs is {Count: 0}) walk.RawEvDefs = null;
            if (walk.RawMultiInstance is {Count: 0}) walk.RawMultiInstance = null;
            if (walk.PriorityAttrNs is {Count: 0}) walk.PriorityAttrNs = null;
            if (walk.FlowNodeAttributes is {Count: 0}) walk.FlowNodeAttributes = null;
            if (walk.ElementsMetadata is {Count: 0}) walk.ElementsMetadata = null;
            if (rootRaw.RawDefinitionsAttr is {Count: 0}) rootRaw.RawDefinitionsAttr = null;
            if (rootRaw.RawProcessAttr is {Count: 0}) rootRaw.RawProcessAttr = null;
            if (rootRaw.RawGlobalElements is {Count: 0}) rootRaw.RawGlobalElements = null;
            if (walk.RawArtifacts is {Count: 0}) walk.RawArtifacts = null;
            if (walk.RawLanes is {Count: 0}) walk.RawLanes = null;
            if (walk.RawDocumentation is {Count: 0}) walk.RawDocumentation = null;
            if (vendorNormalized is {Count: 0}) vendorNormalized = null;
        }

        return new BpmnRawMetadata(
            rootRaw.RawDefinitionsAttr,
            rootRaw.RawProcessAttr,
            Incoming: null,
            Outgoing: null,
            SequenceFlowConditions: walk.RawCond,
            RawExtensionElements: walk.RawExtensions,
            RawEventDefinitions: walk.RawEvDefs?.ToDictionary(k => k.Key, v => (IReadOnlyList<XElement>) v.Value),
            RawMultiInstance: walk.RawMultiInstance?.ToDictionary(k => k.Key, v => new XElement(v.Value)),
            PriorityAttributeNamespace: walk.PriorityAttrNs,
            FlowNodeAttributes: walk.FlowNodeAttributes,
            RoundtripDirty: false,
            NamespacePrefixes: rootRaw.NamespacePrefixes,
            ElementsMetadata: walk.ElementsMetadata,
            RawGlobalElements: rootRaw.RawGlobalElements,
            RawArtifacts: walk.RawArtifacts,
            RawLanes: walk.RawLanes,
            RawDocumentation: walk.RawDocumentation?.ToDictionary(k => k.Key, v => (IReadOnlyList<XElement>) v.Value),
            RawDiRoot: diRoot,
            PartiallyDirtyElements: null,
            GlobalElementKinds: globalKinds,
            VendorNormalizedExtensions: vendorNormalized
        );
    }

    private static void CaptureElementMeta(bool strict,
        Dictionary<string, IReadOnlyDictionary<string, string>>? flowNodeAttributes,
        Dictionary<string, List<XElement>>? rawDocumentation, Dictionary<string, ElementMetadata>? elementsMetadata,
        int orderCounter, XNamespace ns, XElement el
        , string id, bool hadCamundaCollection = false, bool hadZeebeInputCollection = false,
        bool hadLoopCardinality = false, bool hadCamundaElementVar = false,
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

    private Dictionary<string, string>? ExtractExtensions(XElement el, XNamespace ns, bool strict,
        Dictionary<string, XElement>? rawExtensions)
    {
        if (!_options.PreserveUnknownExtensions) return null;
        var extParent = el.Element(ns + "extensionElements") ?? el.Element("extensionElements");
        if (extParent == null) return null;
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (strict && rawExtensions != null)
        {
            var ownerId = el.Attribute("id")?.Value;
            if (!string.IsNullOrEmpty(ownerId))
            {
                rawExtensions[ownerId] = _options.UseLazyRawCloning
                    ? new LazyXElement(extParent).Element
                    : new XElement(extParent);
            }
        }

        void Harvest(XElement node)
        {
            foreach (var attr in node.Attributes())
            {
                var key = $"{node.Name}:{attr.Name.LocalName}";
                dict[key] = attr.Value;
            }

            if (!node.HasAttributes && node != extParent)
            {
                var key = $"{node.Name}:__present";
                dict[key] = "true";
            }

            foreach (var child in node.Elements()) Harvest(child);
        }

        foreach (var top in extParent.Elements()) Harvest(top);
        return dict.Count == 0 ? null : dict;
    }

    private List<BpmnMessage> ParseMessages(List<XElement> messages)
    {
        return messages.Select(m =>
                new BpmnMessage(Intern(m.Attribute("id")?.Value ?? string.Empty), m.Attribute("name")?.Value))
            .Where(m => !string.IsNullOrEmpty(m.Id)).ToList();
    }

    private List<BpmnSignal> ParseSignals(List<XElement> signals)
    {
        return signals.Select(s =>
                new BpmnSignal(Intern(s.Attribute("id")?.Value ?? string.Empty), s.Attribute("name")?.Value))
            .Where(s => !string.IsNullOrEmpty(s.Id)).ToList();
    }

    private List<BpmnError> ParseErrors(List<XElement> errors)
    {
        return errors.Select(e => new BpmnError(Intern(e.Attribute("id")?.Value ?? string.Empty),
                e.Attribute("name")?.Value, e.Attribute("errorCode")?.Value))
            .Where(e => !string.IsNullOrEmpty(e.Id)).ToList();
    }

    private List<BpmnEscalation> ParseEscalations(List<XElement> escalations)
    {
        return escalations.Select(e => new BpmnEscalation(Intern(e.Attribute("id")?.Value ?? string.Empty),
                e.Attribute("name")?.Value, e.Attribute("escalationCode")?.Value))
            .Where(e => !string.IsNullOrEmpty(e.Id)).ToList();
    }

    private List<BpmnEvent> ParseEvents(XElement process, XNamespace ns)
    {
        var res = ParseProcessElements(process, ns, _options.RoundtripMode == BpmnRoundtripMode.Strict,
            ComputeGatewayDefaults(process), CancellationToken.None);
        return res.Events;
    }

    private List<BpmnTask> ParseTasks(XElement process, XNamespace ns)
    {
        var res = ParseProcessElements(process, ns, _options.RoundtripMode == BpmnRoundtripMode.Strict,
            ComputeGatewayDefaults(process), CancellationToken.None);
        return res.Tasks;
    }

    private List<BpmnGateway> ParseGateways(XElement process, XNamespace ns)
    {
        var res = ParseProcessElements(process, ns, _options.RoundtripMode == BpmnRoundtripMode.Strict,
            ComputeGatewayDefaults(process), CancellationToken.None);
        return res.Gateways;
    }

    private List<BpmnSubprocess> ParseSubprocesses(XElement process, XNamespace ns)
    {
        var res = ParseProcessElements(process, ns, _options.RoundtripMode == BpmnRoundtripMode.Strict,
            ComputeGatewayDefaults(process), CancellationToken.None);
        return res.Subprocesses;
    }

    private List<BpmnSequenceFlow> ParseSequenceFlows(XElement process, XNamespace ns)
    {
        var res = ParseProcessElements(process, ns, _options.RoundtripMode == BpmnRoundtripMode.Strict,
            ComputeGatewayDefaults(process), CancellationToken.None);
        return res.Flows;
    }

    private List<BpmnLane> ParseLanes(XElement process, XNamespace ns)
    {
        var res = ParseProcessElements(process, ns, _options.RoundtripMode == BpmnRoundtripMode.Strict,
            ComputeGatewayDefaults(process), CancellationToken.None);
        return res.Lanes;
    }

    private List<BpmnDataObject> ParseDataObjects(XElement process, XNamespace ns)
    {
        var res = ParseProcessElements(process, ns, _options.RoundtripMode == BpmnRoundtripMode.Strict,
            ComputeGatewayDefaults(process), CancellationToken.None);
        return res.DataObjects;
    }

    private List<BpmnAssociation> ParseAssociations(XElement process, XNamespace ns)
    {
        var res = ParseProcessElements(process, ns, _options.RoundtripMode == BpmnRoundtripMode.Strict,
            ComputeGatewayDefaults(process), CancellationToken.None);
        return res.Associations;
    }

    private List<BpmnTextAnnotation> ParseTextAnnotations(XElement process, XNamespace ns)
    {
        var res = ParseProcessElements(process, ns, _options.RoundtripMode == BpmnRoundtripMode.Strict,
            ComputeGatewayDefaults(process), CancellationToken.None);
        return res.TextAnnotations;
    }

    private List<BpmnParticipant> ParseParticipants(XElement collaboration, XNamespace ns)
    {
        var (participants, _) = ParseCollaboration(collaboration.Document!, ns); // reuse collab parser
        return participants;
    }

    private List<BpmnMessageFlow> ParseMessageFlows(XElement collaboration, XNamespace ns)
    {
        var (_, flows) = ParseCollaboration(collaboration.Document!, ns);
        return flows;
    }

    /// <summary>
    /// Extrahiert <properties>/<property>-Strukturen aus beliebigen Namespaces (z.B. camunda:properties).
    /// </summary>
    private static void ExtractPropertiesFromAnyNamespace(XElement extensionElements,
        IDictionary<string, string> attributes)
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
        var dataObjectReferences = model.DataObjectReferences != null
            ? AsSpanSafe(model.DataObjectReferences)
            : Array.Empty<BpmnDataObjectReference>();
        var associations = model.Associations != null ? AsSpanSafe(model.Associations) : Array.Empty<BpmnAssociation>();
        var textAnnotations = model.TextAnnotations != null
            ? AsSpanSafe(model.TextAnnotations)
            : Array.Empty<BpmnTextAnnotation>();
        var groups = model.Groups != null ? AsSpanSafe(model.Groups) : Array.Empty<BpmnGroup>();
        var list = new List<ValidationDiagnostic>();

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
                    AddIfAbsent("REF-GLOBAL-MESSAGE-MISSING", evId,
                        $"Event '{evId}' references unknown message '{refId}'");
            }
            else if (msg.StartsWith("Unknown signalRef '", StringComparison.Ordinal))
            {
                if (TryExtractRef(msg, "Unknown signalRef '", "' at event ", out var refId, out var evId))
                    AddIfAbsent("REF-GLOBAL-SIGNAL-MISSING", evId,
                        $"Event '{evId}' references unknown signal '{refId}'");
            }
            else if (msg.StartsWith("Unknown errorRef '", StringComparison.Ordinal))
            {
                if (TryExtractRef(msg, "Unknown errorRef '", "' at event ", out var refId, out var evId))
                    AddIfAbsent("REF-GLOBAL-ERROR-MISSING", evId, $"Event '{evId}' references unknown error '{refId}'");
            }
            else if (msg.StartsWith("Unknown escalationRef '", StringComparison.Ordinal))
            {
                if (TryExtractRef(msg, "Unknown escalationRef '", "' at event ", out var refId, out var evId))
                    AddIfAbsent("REF-GLOBAL-ESCALATION-MISSING", evId,
                        $"Event '{evId}' references unknown escalation '{refId}'");
            }

            // Link events
            if (msg.StartsWith("Unmatched link ", StringComparison.Ordinal))
            {
                var linkName = msg["Unmatched link ".Length..].Trim();
                if (linkName.Length > 0 &&
                    !list.Exists(d =>
                        d.Code == "SEM-LINK-UNMATCHED" && d.Message.Contains(linkName, StringComparison.Ordinal)))
                {
                    list.Add(new ValidationDiagnostic(
                        Code: "SEM-LINK-UNMATCHED",
                        Severity: ValidationSeverity.Error,
                        Message:
                        $"Link event name '{linkName}' has no matching catch (exactly one throw & one catch required)",
                        ElementId: null,
                        Category: "Semantic"));
                }
            }
            else if (msg.StartsWith("Multiple throw link events for ", StringComparison.Ordinal))
            {
                var linkName = msg["Multiple throw link events for ".Length..].Trim();
                if (linkName.Length > 0 &&
                    !list.Exists(d =>
                        d.Code == "SEM-LINK-MULTIPLE-THROW" && d.Message.Contains(linkName, StringComparison.Ordinal)))
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
        foreach (var e in events)
            if (!string.IsNullOrEmpty(e.Id))
                nodeIds.Add(e.Id);
        foreach (var t in tasks)
            if (!string.IsNullOrEmpty(t.Id))
                nodeIds.Add(t.Id);
        foreach (var g in gateways)
            if (!string.IsNullOrEmpty(g.Id))
                nodeIds.Add(g.Id);
        foreach (var sp in subprocesses)
            if (!string.IsNullOrEmpty(sp.Id))
                nodeIds.Add(sp.Id);

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
        if (rawLaneElements is {Count: > 0})
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
                        !list.Exists(d =>
                            d.Code == "REF-LANE-FLOWNODE-MISSING" && d.ElementId == laneId &&
                            d.Message.Contains(refId, StringComparison.Ordinal)))
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
                if (!string.IsNullOrEmpty(d.Id))
                    dataObjectIds.Add(d.Id);
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
                            Message:
                            $"DataObjectReference '{dref.Id}' references missing dataObject '{dref.DataObjectRef}'",
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
            foreach (var d in dataObjects)
                if (!string.IsNullOrEmpty(d.Id))
                    knownIds.Add(d.Id);
            foreach (var tn in textAnnotations)
                if (!string.IsNullOrEmpty(tn.Id))
                    knownIds.Add(tn.Id);
            foreach (var g in groups)
                if (!string.IsNullOrEmpty(g.Id))
                    knownIds.Add(g.Id);
            foreach (var dref in dataObjectReferences)
                if (!string.IsNullOrEmpty(dref.Id))
                    knownIds.Add(dref.Id);

            foreach (var assoc in associations)
            {
                if (string.IsNullOrEmpty(assoc.Id)) continue;

                var missingSource = string.IsNullOrEmpty(assoc.SourceRef) || !knownIds.Contains(assoc.SourceRef);
                var missingTarget = string.IsNullOrEmpty(assoc.TargetRef) || !knownIds.Contains(assoc.TargetRef);

                if (missingSource)
                {
                    if (!list.Exists(d =>
                            d.Code == "REF-ASSOCIATION-ENDPOINT-MISSING" && d.ElementId == assoc.Id &&
                            d.Message.Contains("sourceRef", StringComparison.OrdinalIgnoreCase)))
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
                    if (!list.Exists(d =>
                            d.Code == "REF-ASSOCIATION-ENDPOINT-MISSING" && d.ElementId == assoc.Id &&
                            d.Message.Contains("targetRef", StringComparison.OrdinalIgnoreCase)))
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
                    if (!eventTypeById.TryGetValue(tgt, out var tType) ||
                        !string.Equals(tType, "intermediateCatchEvent", StringComparison.Ordinal))
                    {
                        if (!list.Exists(d =>
                                d.Code == "SEM-EVENTGW-INVALID-OUTGOING" && d.ElementId == gw.Id &&
                                d.Message.Contains(tgt, StringComparison.Ordinal)))
                        {
                            list.Add(new ValidationDiagnostic(
                                Code: "SEM-EVENTGW-INVALID-OUTGOING",
                                Severity: ValidationSeverity.Error,
                                Message:
                                $"Event-based gateway '{gw.Id}' has outgoing flow '{flow.Id}' to invalid target '{tgt}' (must target intermediateCatchEvent)",
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
                                                                 || !reachable.Contains(f.SourceRef) ||
                                                                 !reachable.Contains(f.TargetRef);
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

        return list;
    }

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? ParseVendorExtensions(bool strict,
        Dictionary<string, XElement>? rawExtensions)
    {
        // NEW Phase B: vendor extension normalization capture (expanded all vendors + generics)
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? vendorNormalized = null;
        if (strict && _options.NormalizeVendorExtensions && rawExtensions is {Count: > 0})
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
                                var idKey = string.IsNullOrEmpty(child.Attribute("id")?.Value)
                                    ? NextIndexedKey(bucket, "cib:connector")
                                    : $"cib:connector.{cid}";
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
                    e => (IReadOnlyDictionary<string, string>) new ReadOnlyDictionary<string, string>(e.Value),
                    StringComparer.Ordinal);
            }
        }

        return vendorNormalized;
    }

    public string Serialize(BpmnModel model)
    {
        // Phase 8: Use NormalizedProjectionSerializer when enabled
        if (_options.EnableNormalizedProjectionSerializer)
        {
            var normalizedSerializer = new NormalizedProjectionSerializer(_options);
            return normalizedSerializer.Serialize(model);
        }

        // Existing behavior: use BpmnSerializer for strict/roundtrip mode
        return new BpmnSerializer {RoundtripMode = _options.RoundtripMode}.Serialize(model);
    }

    private (LoopCharacteristics? loop, bool conflict) ParseLoopLocal(XElement sp, XNamespace ns,
        HashSet<string> pendingMiConflicts)
    {
        var res = ParseLoopWithConflict(sp, ns, pendingMiConflicts);
        if (res.conflict) pendingMiConflicts.Add(sp.Attribute("id")?.Value ?? "");
        return res;
    }

    private static (LoopCharacteristics? loop, bool conflict) ParseLoopWithConflict(XElement sp, XNamespace ns,
        HashSet<string> conflictSet)
    {
        var mi = sp.Element(ns + "multiInstanceLoopCharacteristics") ?? sp.Element("multiInstanceLoopCharacteristics");
        if (mi != null)
        {
            bool isSeq = mi.Attribute("isSequential")?.Value == "true";
            int? card = null;
            var cardText = mi.Element(ns + "loopCardinality")?.Value ?? mi.Element("loopCardinality")?.Value;
            if (int.TryParse(cardText, out var cParsed)) card = cParsed;
            var camundaCollection = mi.Attribute(XName.Get("collection", "http://camunda.org/schema/1.0/bpmn"))?.Value;
            var zeebeCollection = mi.Element(XName.Get("inputCollection", "http://zeebe.io/schema/zeebe/1.0"))?.Value;
            var collectionRaw = camundaCollection ?? zeebeCollection;
            var camundaElementVar =
                mi.Attribute(XName.Get("elementVariable", "http://camunda.org/schema/1.0/bpmn"))?.Value;
            var zeebeInputElement = mi.Element(XName.Get("inputElement", "http://zeebe.io/schema/zeebe/1.0"))?.Value;
            var zeebeOutputElement = mi.Element(XName.Get("outputElement", "http://zeebe.io/schema/zeebe/1.0"))?.Value;
            var elementVar = camundaElementVar ?? zeebeInputElement ?? zeebeOutputElement;
            var completion = mi.Element(ns + "completionCondition")?.Value ?? mi.Element("completionCondition")?.Value;
            bool conflict = !string.IsNullOrWhiteSpace(collectionRaw) && card.HasValue;
            if (!string.IsNullOrWhiteSpace(collectionRaw)) card = null;
            var loop = new MultiInstanceLoopCharacteristics(isSeq, card, collectionRaw, elementVar, completion,
                zeebeInputElement, zeebeOutputElement);
            return (loop, conflict);
        }

        var std = sp.Element(ns + "standardLoopCharacteristics") ?? sp.Element("standardLoopCharacteristics");
        if (std != null)
        {
            var loopCond = std.Element(ns + "loopCondition")?.Value ?? std.Element("loopCondition")?.Value;
            bool testBefore = std.Attribute("testBefore")?.Value == "true";
            int? loopMax = null;
            if (int.TryParse(std.Attribute("loopMaximum")?.Value, out var lm)) loopMax = lm;
            return (new StandardLoopCharacteristics(loopCond, testBefore, loopMax), false);
        }

        return (null, false);
    }

    private static IReadOnlyList<EventDefinition> ParseEventDefinitions(XElement evt, XNamespace ns)
    {
        // For backward compatibility, use basic options without validation
        var defaultOptions = new BpmnParserOptions {ValidateEventDefinitions = false};
        var (definitions, _) = ParseEventDefinitionsWithDiagnostics(evt, ns, defaultOptions);
        return definitions;   
    }

    private static void MaybeThrowOnValidation(BpmnParserOptions options,
        IReadOnlyList<ValidationDiagnostic> diagnostics)
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
    private static (IReadOnlyList<EventDefinition> definitions, IReadOnlyList<ValidationDiagnostic> diagnostics)
        ParseEventDefinitionsWithDiagnostics(XElement evt, XNamespace ns, BpmnParserOptions options)
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
                    //Unknown/vendor event definition detected
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
                            Message:
                            $"Event '{eventId}' contains unknown event definition '{elementDisplayName}' (preserved in raw form)",
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
                        Message:
                        $"Event '{eventId}' contains unknown event definition '{elementDisplayName}' (preserved in raw form)",
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
                defElem.Element(ns + "conditionExpression")?.Value ??
                defElem.Element("conditionExpression")?.Value ?? string.Empty),

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

    // Phase 8: Add structural model hashing for cache invalidation
    public string ComputeStructuralModelHash(BpmnModel model)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));

        var structuralContent = new StringBuilder();

        // Phase 8: Build deterministic representation of structural content
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
        return model with {Tasks = updatedTasks};
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

                    updatedTasks.Add(task with {Attributes = (Dictionary<string, string>) combinedAttributes});
                }
            }

            updatedTasks.Add(task);
        }

        return updatedTasks;
    }

    private void FinalizeValidationAndTracing(Activity? activity, BpmnModel model)
    {
        // Set tracing tags (safe even for empty model)
        if (activity != null)
        {
            activity.SetTag("bpmn.process_id", model.ProcessId ?? string.Empty);
            activity.SetTag("bpmn.node_count",
                ((model.Events?.Count ?? 0)
                 + (model.Tasks?.Count ?? 0)
                 + (model.Gateways?.Count ?? 0)
                 + (model.Subprocesses?.Count ?? 0)).ToString());
            activity.SetTag("bpmn.flow_count", (model.SequenceFlows?.Count ?? 0).ToString());
            activity.SetTag("bpmn.roundtrip_mode", _options.RoundtripMode.ToString());
            activity.SetTag("bpmn.runtime_projection", _options.BuildRuntimeProjection.ToString().ToLowerInvariant());
            activity.SetTag("bpmn.vendor_normalization",
                _options.NormalizeVendorExtensions.ToString().ToLowerInvariant());

            var structured = model.ValidationDiagnostics;
            if (structured != null)
            {
                activity.SetTag("bpmn.validation_errors",
                    structured.Count(d => d.Severity >= ValidationSeverity.Error).ToString());
                activity.SetTag("bpmn.validation_warnings",
                    structured.Count(d => d.Severity == ValidationSeverity.Warning).ToString());
            }
            else
            {
                activity.SetTag("bpmn.validation_errors", "0");
                activity.SetTag("bpmn.validation_warnings", "0");
            }
        }

        // Optional: keep consistent end-of-phase log
        if (_options.EnableLogging)
        {
            _logger.LogDebug(
                "PhaseComplete: ProcessId={ProcessId}, ParsedSuccessfully=true, DiagnosticsCount={DiagnosticsCount}",
                model.ProcessId,
                model.Diagnostics?.Count ?? 0);
        }
    }

    // Encapsulates all collections and capture from the process walk
    private sealed class ProcessParseResult
    {
        public List<BpmnEvent> Events { get; } = new();
        public List<BpmnGateway> Gateways { get; } = new();
        public List<BpmnSubprocess> Subprocesses { get; } = new();
        public List<BpmnSequenceFlow> Flows { get; } = new();
        public List<BpmnTask> Tasks { get; } = new();
        public List<BpmnDataObject> DataObjects { get; } = new();
        public List<BpmnDataObjectReference> DataObjectRefs { get; } = new();
        public List<BpmnDataStore> DataStores { get; } = new();
        public List<BpmnDataStoreReference> DataStoreRefs { get; } = new();
        public List<BpmnProperty> Properties { get; } = new();
        public List<BpmnActivityIo> ActivityIo { get; } = new();
        public List<BpmnLane> Lanes { get; } = new();
        public List<BpmnTextAnnotation> TextAnnotations { get; } = new();
        public List<BpmnAssociation> Associations { get; } = new();
        public List<BpmnGroup> Groups { get; } = new();

        // Captures and working sets (from strict mode and validations)
        public Dictionary<string, XElement>? RawExtensions { get; set; }
        public Dictionary<string, List<XElement>>? RawEvDefs { get; set; }
        public Dictionary<string, (string Raw, bool WasCData)>? RawCond { get; set; }
        public Dictionary<string, IReadOnlyDictionary<string, string>>? FlowNodeAttributes { get; set; }
        public Dictionary<string, List<XElement>>? RawDocumentation { get; set; }
        public Dictionary<string, XElement>? RawMultiInstance { get; set; }
        public Dictionary<string, string>? PriorityAttrNs { get; set; }
        public Dictionary<string, ElementMetadata>? ElementsMetadata { get; set; }
        public List<XElement>? RawArtifacts { get; set; }
        public List<XElement>? RawLanes { get; set; }

        public HashSet<string> FlowNodeIds { get; } = new(StringComparer.Ordinal);
        public HashSet<string> IdIndex { get; } = new(StringComparer.Ordinal);
        public HashSet<string> PendingMiConflicts { get; } = new(StringComparer.Ordinal);
        public HashSet<string> TransactionIds { get; } = new(StringComparer.Ordinal);
        public List<(string Id, string? Attached)> BoundaryEvents { get; } = new();
        public Dictionary<string, int> LinkThrowCounts { get; } = new(StringComparer.Ordinal);
        public HashSet<string> LinkCatchNames { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string> PotentialOwnerExtras { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, (string? Format, string? Body, string? Result)> ScriptTaskRaw { get; } =
            new(StringComparer.Ordinal);

        public List<ValidationDiagnostic> UnknownEventDefinitionDiagnostics { get; } = new();
    }
}

public partial class RefactoredBpmnParser
{
    /// <summary>
    /// Capabilities exposed by the roundtrip parser (updated based on implemented phases).
    /// </summary>
    public static readonly BpmnParserCapabilities Capabilities =
        new(
            SupportsStrictRoundtrip: true,
            SupportsRuntimeProjection: true, // Phase 4 - implemented
            SupportsCollaboration: false, // Phase 1 - not yet implemented
            SupportsVendorNormalization: true, // Phase 2 - implemented
            SupportsAdvancedValidation: true // Phase 3 - implemented
        );
}