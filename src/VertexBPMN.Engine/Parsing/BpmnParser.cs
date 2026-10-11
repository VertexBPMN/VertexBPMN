using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
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
	private readonly Dictionary<string, LinkedListNode<(string Key, BpmnModel Model)>> _cacheIndex = new(StringComparer.Ordinal);
	private readonly LinkedList<(string Key, BpmnModel Model)> _lru = new();
	private readonly System.Threading.Lock _cacheLock = new();

	//Observability Infrastructure
	private readonly ActivitySource? _activitySource;
	private readonly ILogger<BpmnParser> _logger;
	private static readonly ActivitySource DefaultActivitySource = new("VertexBPMN.Engine.Parsing");


	private sealed record GatewayTraversalInfo(string Id, string Type, string? DefaultId);

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
		ArgumentNullException.ThrowIfNull(tracerProvider);
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
		if (_options.CacheSize <= 0)
		{
			return null;
		}

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
		if (_options.CacheSize <= 0)
		{
			return;
		}

		var key = Hash(xml);
		lock (_cacheLock)
		{
			if (_cacheIndex.ContainsKey(key))
			{
				return;
			}

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

	public async Task<BpmnModel> ParseAsync(string bpmnXml, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(bpmnXml);

		var resourceLimiter = new BpmnResourceLimiter(_options.SecurityOptions);
		var limitResult = resourceLimiter.ValidateInputLimits(bpmnXml);
		if (!limitResult.IsValid)
		{
			throw new SecurityException(
				$"BPMN input exceeds resource limits: {string.Join("; ", limitResult.Violations)}");
		}

		if (_options.EnableSecurityValidation)
		{
			var securityValidator = new BpmnSecurityValidator(_options.SecurityOptions);
			var securityResult = securityValidator.ValidateSecurityConfiguration(bpmnXml);
			if (_options.FailOnSecurityThreat && !securityResult.IsSecure)
			{
				throw new SecurityException(
					$"BPMN input failed security validation: {string.Join("; ", securityResult.Vulnerabilities)}");
			}
		}

		return await resourceLimiter.ExecuteWithResourceLimitsAsync(async parseToken =>
		{
			if (_options.EnableStreamingParse && bpmnXml.Length > _options.StreamingThreshold)
			{
				var streamingParser = new BpmnStreamingParser(_options);
				var streamingModel = await streamingParser.ParseAsync(bpmnXml, parseToken);
				return ApplyPostProcessing(streamingModel);
			}

			var model = await Parse(bpmnXml, parseToken);
			return ApplyPostProcessing(model);
		}, cancellationToken);
	}

	private Task<BpmnModel> Parse(string xml, CancellationToken cancellationToken = default)
	{
		using var activity = _activitySource?.StartActivity("BpmnParser.ParseAsync");

		if (ReadLoggedCache(xml) is { } cached)
		{
			return Task.FromResult(cached);
		}

		// ParseStart logging
		if (_options.EnableLogging && _logger.IsEnabled(LogLevel.Debug))
		{
			_logger.LogDebug("ParseStart: RoundtripMode={RoundtripMode}, BuildRuntimeProjection={BuildRuntimeProjection}, NormalizeVendorExtensions={NormalizeVendorExtensions}",
				_options.RoundtripMode, _options.BuildRuntimeProjection, _options.NormalizeVendorExtensions);
		}

		var strict = _options.RoundtripMode == BpmnRoundtripMode.Strict;
		var capture = new DefinitionCapture(strict);







		// original loop characteristics node
		// priority attribute namespace per sequenceFlow
		// attribute snapshot per flow node / sequenceFlow
		// NEW
		// NEW

		var diagnostics = new List<string>();
		using var secureReader = new BpmnResourceLimiter(_options.SecurityOptions)
			.CreateSecureXmlReader(xml, cancellationToken);
		var doc = XDocument.Load(secureReader, LoadOptions.PreserveWhitespace);
		ExternalTaskValidation.ValidateXml(doc);
		var root = doc.Root!;
		var ns = root.Name.Namespace;
		CaptureDefinitionsMetadata(strict, capture.RawDefinitionsAttr, capture.NamespacePrefixes, root);

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
			return Task.FromResult(CreateEmptyProcessModel(xml, strict, capture.RawDefinitionsAttr, capture.RawProcessAttr, diagnostics));
		}

		return Task.FromResult(ParseProcessDocument(xml, activity, doc, process, allProcesses, strict, capture, diagnostics, cancellationToken));
	}

	private BpmnModel ParseProcessDocument(
		string xml, System.Diagnostics.Activity? activity, XDocument doc, XElement process, List<XElement> allProcesses,
		bool strict, DefinitionCapture capture, List<string> diagnostics, CancellationToken cancellationToken)
	{
		var root = doc.Root!;
		var ns = root.Name.Namespace;
		var pid = Intern(process.Attribute("id")?.Value ?? string.Empty);
		var pname = Intern(process.Attribute("name")?.Value ?? string.Empty);
		var state = CreateTraversalState(process, root, allProcesses, pid, strict, diagnostics, cancellationToken);
		CaptureProcessMetadata(strict, capture.RawProcessAttr, state.RawDocumentation, ns, process);

		var globals = ReadGlobalDefinitions(doc, ns, strict, capture);
		Walk(process, state);

		// Share traversal state across collaboration pools. Each completed traversal
		// restores its subprocess stack before the next top-level process is visited.
		WalkAdditionalProcesses(allProcesses, pid, state);

		// Reference resolution diagnostics
		ValidateGlobalEventReferences(globals.Messages, globals.Signals, globals.Errors, globals.Escalations, state);

		ValidateTraversalActivityRules(state);
		ValidateTraversalLinkRules(state);
		List<BpmnShape>? shapes = null;
		List<BpmnEdge>? edges = null;
		(shapes, edges, capture.DiRoot) = ReadDiagramInterchange(doc, state);
		PopulateSubprocessChildren(state);
		var participants = new List<BpmnParticipant>();
		var lanes = new List<BpmnLane>();
		var messageFlows = new List<BpmnMessageFlow>();
		var participantsList = participants;
		var messageFlowsList = messageFlows;

		ReadCollaborationParticipants(doc, state, participantsList, messageFlowsList);

		var globalKinds = BuildGlobalElementKinds(state, capture);
		var model = CreateCompletedBpmnModel(xml, pid, pname, state, capture, globals, shapes, edges,
			participants, lanes, messageFlows, globalKinds);
		var structuredDiagnostics = ValidateCompletedModel(model, state, pid);
		model.ValidationDiagnostics = structuredDiagnostics;

		LogCompletedProjection(activity, pid, state, model.Runtime, structuredDiagnostics);
		Cache(xml, model);

		return model;
	}

	private BpmnModel? ReadLoggedCache(string xml)
	{
		if (TryGetCached(xml) is { } cached)
		{
			// Log cache hit (only if logging enabled)
			if (_options.EnableLogging && _logger.IsEnabled(LogLevel.Debug))
			{
				_logger.LogDebug("ParseAsync cache hit for XML hash {XmlHash}", Hash(xml)[..8]);
			}

			return cached;
		}

		return null;
	}

	private sealed record ParsedGlobalDefinitions(
		List<BpmnMessage> Messages,
		List<BpmnSignal> Signals,
		List<BpmnError> Errors,
		List<BpmnEscalation> Escalations);

	private ParsedGlobalDefinitions ReadGlobalDefinitions(XDocument doc, XNamespace ns, bool strict, DefinitionCapture capture)
	{
		// global elements capture
		var messages = doc.Descendants(ns + "message").ToList();
		var signals = doc.Descendants(ns + "signal").ToList();
		var errors = doc.Descendants(ns + "error").ToList();
		var escalations = doc.Descendants(ns + "escalation").ToList();
		if (strict)
		{
			foreach (var g in messages.Concat(signals).Concat(errors).Concat(escalations))
			{
				capture.RawGlobalElements!.Add(new XElement(g));
			}
		}

		var messageModels = ParseMessages(messages);
		var signalModels = ParseSignals(signals);
		var errorModels = ParseErrors(errors);
		var escalationModels = ParseEscalations(escalations);
		// FIX: gatewaysRaw wurde bisher nur aus "process" (dem ersten Prozess) berechnet.
		// Damit Default-Flows von Gateways in weiteren Prozessen (anderen Pools) korrekt
		// erkannt werden, jetzt über alle Prozesse hinweg sammeln.

		return new(messageModels, signalModels, errorModels, escalationModels);
	}

	private IReadOnlyDictionary<string, string>? BuildGlobalElementKinds(TraversalState state, DefinitionCapture capture)
	{
		IReadOnlyDictionary<string, string>? globalKinds = null;
		if (state.Strict && _options.BuildGlobalElementIndex && capture.RawGlobalElements is { Count: > 0 })
		{
			var dict = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var x in capture.RawGlobalElements)
			{
				var idAttr = x.Attribute("id")?.Value;
				if (string.IsNullOrEmpty(idAttr))
				{
					continue;
				}

				var local = x.Name.LocalName; // message | signal | error | escalation
				dict[idAttr] = local;
			}
			globalKinds = dict;
		}

		return globalKinds;
	}

	private BpmnModel CreateCompletedBpmnModel(
		string xml, string pid, string pname, TraversalState state, DefinitionCapture capture,
		ParsedGlobalDefinitions globals, List<BpmnShape>? shapes, List<BpmnEdge>? edges,
		List<BpmnParticipant> participants, List<BpmnLane> lanes, List<BpmnMessageFlow> messageFlows,
		IReadOnlyDictionary<string, string>? globalKinds)
	{
		var vendorNormalized = ParseVendorExtensions(state.Strict, state.RawExtensions);

		vendorNormalized = MergePotentialOwnerExtensions(state, vendorNormalized);
		var activities = state.Tasks.Cast<object>().Concat(state.Subprocesses);


		var (rawMeta, normalizedExtensions) = BuildRawCapture(xml, capture, state, globalKinds, vendorNormalized);
		vendorNormalized = normalizedExtensions;
		RuntimeProcessModel? runtime = null;
		if (_options.BuildRuntimeProjection)
		{
			runtime = RuntimeProjectionBuilder.Build(
				_options,
				pid,
				state.Events, state.Tasks, state.Gateways, state.Subprocesses, state.Flows,
				vendorNormalized,
				rawMeta,
				state.ScriptTaskRaw,
				state.PotentialOwnerExtras
			);
		}

		var model = new BpmnModel(pid, pname, state.Events, state.Gateways, state.Subprocesses, state.Flows, state.Tasks, state.DataObjects, state.DataObjectRefs,
			state.DataStores, state.DataStoreRefs, state.Properties, state.ActivityIo, globals.Messages, globals.Signals, globals.Errors,
			globals.Escalations, state.Diagnostics, shapes, edges, participants, lanes, messageFlows, state.TextAnnotations,
			state.AssociationArtifacts, state.Groups, Activities: activities, RawMetadata: rawMeta)
		{
			Runtime = runtime // set mutable property
		};

		return model;
	}

	private (BpmnRawMetadata? Raw, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? VendorExtensions) BuildRawCapture(
		string xml, DefinitionCapture capture, TraversalState state,
		IReadOnlyDictionary<string, string>? globalKinds,
		IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? vendorNormalized)
	{
		BpmnRawMetadata? rawMeta = null;
		if (state.Strict)
		{
			vendorNormalized = OptimizeRawCapture(capture, state, vendorNormalized);
			rawMeta = new BpmnRawMetadata(capture.RawDefinitionsAttr, capture.RawProcessAttr,
				capture.RawIncoming?.ToDictionary(k => k.Key, v => (IReadOnlyList<string>)v.Value, StringComparer.Ordinal),
				capture.RawOutgoing?.ToDictionary(k => k.Key, v => (IReadOnlyList<string>)v.Value, StringComparer.Ordinal),
				state.RawCond?.ToDictionary(k => k.Key, v => v.Value, StringComparer.Ordinal),
				state.RawExtensions,
				state.RawEvDefs?.ToDictionary(k => k.Key, v => (IReadOnlyList<XElement>)v.Value, StringComparer.Ordinal),
				state.RawMultiInstance?.ToDictionary(k => k.Key, v => new XElement(v.Value), StringComparer.Ordinal),
				state.PriorityAttrNs?.ToDictionary(k => k.Key, v => v.Value, StringComparer.Ordinal),
				state.FlowNodeAttributes, false,
				capture.NamespacePrefixes,
				state.ElementsMetadata,
				capture.RawGlobalElements,
				state.RawArtifacts,
				state.RawLanes,
				state.RawDocumentation?.ToDictionary(k => k.Key, v => (IReadOnlyList<XElement>)v.Value, StringComparer.Ordinal),
				RawDiRoot: capture.DiRoot,
				PartiallyDirtyElements: null,
				GlobalElementKinds: globalKinds,
				VendorNormalizedExtensions: vendorNormalized,
				OriginalXml: xml
			);
		}

		return (rawMeta, vendorNormalized);
	}

	private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? OptimizeRawCapture(
		DefinitionCapture capture, TraversalState state,
		IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? vendorNormalized)
	{
		if (_options.OptimizeStrictMemory)
		{
			capture.RawIncoming = OmitEmptyCollection(capture.RawIncoming, capture.RawIncoming?.Count);

			capture.RawOutgoing = OmitEmptyCollection(capture.RawOutgoing, capture.RawOutgoing?.Count);

			state.RawCond = OmitEmptyCollection(state.RawCond, state.RawCond?.Count);

			state.RawExtensions = OmitEmptyCollection(state.RawExtensions, state.RawExtensions?.Count);

			state.RawEvDefs = OmitEmptyCollection(state.RawEvDefs, state.RawEvDefs?.Count);

			capture.RawDefinitionsAttr = OmitEmptyCollection(capture.RawDefinitionsAttr, capture.RawDefinitionsAttr?.Count);

			capture.RawProcessAttr = OmitEmptyCollection(capture.RawProcessAttr, capture.RawProcessAttr?.Count);

			capture.RawGlobalElements = OmitEmptyCollection(capture.RawGlobalElements, capture.RawGlobalElements?.Count);

			state.RawArtifacts = OmitEmptyCollection(state.RawArtifacts, state.RawArtifacts?.Count);

			state.RawLanes = OmitEmptyCollection(state.RawLanes, state.RawLanes?.Count);

			capture.NamespacePrefixes = OmitEmptyCollection(capture.NamespacePrefixes, capture.NamespacePrefixes?.Count);

			state.ElementsMetadata = OmitEmptyCollection(state.ElementsMetadata, state.ElementsMetadata?.Count);

			state.RawDocumentation = OmitEmptyCollection(state.RawDocumentation, state.RawDocumentation?.Count);

			state.RawMultiInstance = OmitEmptyCollection(state.RawMultiInstance, state.RawMultiInstance?.Count);

			state.PriorityAttrNs = OmitEmptyCollection(state.PriorityAttrNs, state.PriorityAttrNs?.Count);

			state.FlowNodeAttributes = OmitEmptyCollection(state.FlowNodeAttributes, state.FlowNodeAttributes?.Count);

			vendorNormalized = OmitEmptyCollection(vendorNormalized, vendorNormalized?.Count);
		}

		return vendorNormalized;
	}

	private BpmnModel CreateEmptyProcessModel(string xml, bool strict,
		Dictionary<string, string>? rawDefinitionsAttr, Dictionary<string, string>? rawProcessAttr,
		List<string> diagnostics)
	{
		if (_options.StrictValidation)
		{
			diagnostics.Add("No <process> element");
		}

		var rawMeta0 = strict ? new BpmnRawMetadata(rawDefinitionsAttr, rawProcessAttr, OriginalXml: xml) : null;

		var empty = new BpmnModel(
			 string.Empty,
			 string.Empty,
			[],
			[],
			[],
			[],
			[],
			[],
			[],
			[],
			[],
			[],
			[],
			[],
			[],
			[],
			[],
			diagnostics,
			RawMetadata: rawMeta0
		)
		{
			Runtime = null
		};
		IReadOnlyList<ValidationDiagnostic>? structured = null;
		if (strict && _options.EnableAdvancedValidation)
		{
			structured = ValidateModel(empty);
		}
		if (_options.EnableAdvancedValidation &&
			_options.ThrowOnFatalValidation &&
			structured is { Count: > 0 })
		{
			MaybeThrowOnValidation(_options, structured);
		}

		empty.ValidationDiagnostics = structured;

		Cache(xml, empty);
		return empty;
	}

	private List<ValidationDiagnostic>? ValidateCompletedModel(BpmnModel model, TraversalState state, string pid)
	{
		List<ValidationDiagnostic>? structuredDiagnostics = null;
		if (_options.EnableAdvancedValidation)
		{
			structuredDiagnostics = ValidateModel(model);

			//Merge unknown event definition diagnostics
			if (state.UnknownEventDefinitionDiagnostics.Count > 0)
			{
				var allDiagnostics = new List<ValidationDiagnostic>(structuredDiagnostics);
				allDiagnostics.AddRange(state.UnknownEventDefinitionDiagnostics);
				structuredDiagnostics = allDiagnostics;
			}

			//ValidationSummary logging
			if (_options.EnableLogging && _logger.IsEnabled(LogLevel.Information) && structuredDiagnostics.Count > 0)
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
		return structuredDiagnostics;
	}

	private void LogCompletedProjection(System.Diagnostics.Activity? activity, string pid, TraversalState state,
		RuntimeProcessModel? runtime, List<ValidationDiagnostic>? structuredDiagnostics)
	{
		// Phase 5: ProjectionBuilt logging
		if (_options.EnableLogging && _logger.IsEnabled(LogLevel.Debug) && runtime != null)
		{
			_logger.LogDebug("ProjectionBuilt: ProcessId={ProcessId}, FlowNodeCount={FlowNodeCount}, SequenceFlowCount={SequenceFlowCount}, ScriptTaskCount={ScriptTaskCount}, PotentialOwnerCount={PotentialOwnerCount}",
				pid, runtime.FlowNodes.Count, runtime.SequenceFlows.Count,
				runtime.ScriptTasks?.Count ?? 0, runtime.PotentialOwners?.Count ?? 0);
		}

		// Phase 5: Add span attributes (only if tracing enabled)
		RecordParseActivity(activity, pid, state, structuredDiagnostics);

		if (_options.EnableLogging && _logger.IsEnabled(LogLevel.Debug))
		{
			_logger.LogDebug("PhaseComplete: ProcessId={ProcessId}, ParsedSuccessfully=true, DiagnosticsCount={DiagnosticsCount}",
				pid, state.Diagnostics.Count);
		}

	}

	private TraversalState CreateTraversalState(
		XElement process, XElement root, List<XElement> allProcesses,
		string pid, bool strict, List<string> diagnostics, CancellationToken cancellationToken)
	{
		var gatewaysRaw = allProcesses.SelectMany(p => p.Descendants())
			.Where(e => e.Name.LocalName.EndsWith("Gateway", StringComparison.Ordinal)).Select(g =>
			new GatewayTraversalInfo(Intern(g.Attribute("id")?.Value ?? string.Empty), g.Name.LocalName, g.Attribute("default")?.Value)).ToList();
		return new TraversalState
		{
			OrderCounter = 0,
			SubprocessStack = new Stack<string>(),
			IdIndex = new HashSet<string>(StringComparer.Ordinal),
			Diagnostics = diagnostics,
			Ns = root.Name.Namespace,
			Strict = strict,
			RawExtensions = strict && !(IsLargeModel(process, _options) && _options.SkipExtensionsForLargeModels) ? new Dictionary<string, XElement>(StringComparer.Ordinal) : null,
			PendingMiConflicts = new HashSet<string>(StringComparer.Ordinal),
			RawMultiInstance = strict ? new Dictionary<string, XElement>(StringComparer.Ordinal) : null,
			Subprocesses = [],
			CurrentProcessId = pid,
			TransactionIds = new HashSet<string>(StringComparer.Ordinal),
			FlowNodeIds = new HashSet<string>(StringComparer.Ordinal),
			FlowNodeAttributes = strict ? new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal) : null,
			RawDocumentation = strict && !(IsLargeModel(process, _options) && _options.SkipDocumentationForLargeModels) ? new Dictionary<string, List<XElement>>(StringComparer.Ordinal) : null,
			ElementsMetadata = strict ? new Dictionary<string, ElementMetadata>(StringComparer.Ordinal) : null,
			UnknownEventDefinitionDiagnostics = [],
			Events = [],
			BoundaryEvents = [],
			LinkThrowCounts = new Dictionary<string, int>(StringComparer.Ordinal),
			LinkCatchNames = new HashSet<string>(StringComparer.Ordinal),
			RawEvDefs = strict ? new Dictionary<string, List<XElement>>(StringComparer.Ordinal) : null,
			ScriptTaskRaw = new Dictionary<string, (string? Format, string? Body, string? Result)>(StringComparer.Ordinal),
			PotentialOwnerExtras = new Dictionary<string, string>(StringComparer.Ordinal),
			Tasks = [],
			ActivityIo = [],
			Gateways = [],
			GatewaysRaw = gatewaysRaw,
			RawLanes = strict ? [] : null,
			PriorityAttrNs = strict ? new Dictionary<string, string>(StringComparer.Ordinal) : null,
			Root = root,
			Flows = [],
			DefaultIds = new HashSet<string>(gatewaysRaw.Select(g => g.DefaultId).Where(v => !string.IsNullOrWhiteSpace(v))!, StringComparer.Ordinal),
			RawCond = strict ? new Dictionary<string, (string Raw, bool WasCData)>(StringComparer.Ordinal) : null,
			DataObjects = [],
			DataObjectRefs = [],
			DataStores = [],
			DataStoreRefs = [],
			Properties = [],
			TextAnnotations = [],
			RawArtifacts = strict && !(IsLargeModel(process, _options) && _options.SkipArtifactsForLargeModels) ? [] : null,
			Groups = [],
			AssociationArtifacts = [],
			CancellationToken = cancellationToken,
		};
	}

	private static void CaptureElementMeta(bool strict, Dictionary<string, IReadOnlyDictionary<string, string>>? flowNodeAttributes,
		Dictionary<string, List<XElement>>? rawDocumentation, Dictionary<string, ElementMetadata>? elementsMetadata, int orderCounter, XNamespace ns, XElement el
	   , string id, bool hadCamundaCollection = false, bool hadZeebeInputCollection = false, bool hadLoopCardinality = false, bool hadCamundaElementVar = false,
		bool hadZeebeInputElement = false, bool hadZeebeOutputElement = false)
	{
		if (!strict || string.IsNullOrEmpty(id))
		{
			return;
		}

		var attrDict = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var a in el.Attributes())
		{
			if (a.IsNamespaceDeclaration)
			{
				continue;
			}

			attrDict[a.Name.ToString()] = a.Value;
		}

		elementsMetadata![id] = new ElementMetadata(orderCounter, el.Name.LocalName, attrDict, hadCamundaCollection,
			hadZeebeInputCollection, hadLoopCardinality, hadCamundaElementVar, hadZeebeInputElement,
			hadZeebeOutputElement);

		flowNodeAttributes?[id] = attrDict;
		if (rawDocumentation != null)
		{
			var docs = el.Elements(ns + "documentation")
				.Concat(el.Elements("documentation")).ToList();
			if (docs.Count > 0)
			{
				if (!rawDocumentation.TryGetValue(id, out var list))
				{
					list = [];
					rawDocumentation[id] = list;
				}

				foreach (var d in docs)
				{
					list.Add(new XElement(d));
				}
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
			{
				attributes[name] = value;
			}
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
			FlattenTaskExecutionExtensions(extensionElements, attributes);
		}

		if (element.Attribute("scriptFormat")?.Value is { } scriptFormat)
		{
			attributes["scriptFormat"] = scriptFormat;
		}

		if ((element.Element(bpmnNamespace + "script") ?? element.Element("script"))?.Value is { } script)
		{
			attributes["script"] = script;
		}

		if (element.Attribute("resultVariable")?.Value is { } resultVariable)
		{
			attributes["resultVariable"] = resultVariable;
		}

		if (element.Attribute("calledElement")?.Value is { } calledElement)
		{
			attributes["calledElement"] = calledElement;
		}

		if (element.Attribute("decisionRef")?.Value is { } decisionRef)
		{
			attributes["decisionRef"] = decisionRef;
		}

		// Standard execution semantics must survive runtime projection, not only strict XML roundtrips.
		if (element.Attribute("isForCompensation")?.Value is { } compensation)
		{
			attributes["isForCompensation"] = compensation;
		}

		return attributes.Count == 0 ? null : attributes;
	}
	private static void FlattenTaskExecutionExtensions(XElement extensionElements, Dictionary<string, string> attributes)
	{
		foreach (var header in extensionElements.Descendants().Where(element => element.Name.LocalName == "header"
			&& element.Name.NamespaceName is BpmnXmlNamespaces.Zeebe or BpmnXmlNamespaces.CamundaZeebe))
		{
			var key = header.Attribute("key")?.Value;
			var value = header.Attribute("value")?.Value;
			if (!string.IsNullOrWhiteSpace(key) && value != null)
			{
				attributes[$"zeebe:taskHeaders.{key}"] = value;
			}
		}
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
			{
				attributes[name] = value;
			}
		}

		foreach (var mcpTask in extensionElements.Descendants().Where(e => e.Name.LocalName == "mcpServiceTask"))
		{
			foreach (var attribute in mcpTask.Attributes().Where(a => !a.IsNamespaceDeclaration))
			{
				attributes[attribute.Name.LocalName] = attribute.Value;
			}
		}

		const string flowableNamespace = BpmnXmlNamespaces.Flowable;
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
		{
			attributes["flowable:taskListeners"] = JsonSerializer.Serialize(listeners);
		}
	}


	private Dictionary<string, string>? ExtractExtensions(XElement el, XNamespace ns, bool strict, Dictionary<string, XElement>? rawExtensions)
	{
		if (!_options.PreserveUnknownExtensions)
		{
			return null;
		}

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


		if (extParent != null)
		{
			foreach (var top in extParent.Elements())
			{
				HarvestExtension(top, extParent, dict);
			}
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

	private static string FormatQualifiedName(XElement node, string? prefix)
	{
		if (!string.IsNullOrEmpty(prefix))
		{
			return $"{prefix}:{node.Name.LocalName}";
		}

		var namespaceUri = node.Name.Namespace.NamespaceName;
		return string.IsNullOrEmpty(namespaceUri)
			? node.Name.LocalName
			: $"{{{namespaceUri}}}{node.Name.LocalName}";
	}

	private static string FormatQualifiedAttributeName(XAttribute attribute, string? prefix)
	{
		if (!string.IsNullOrEmpty(prefix))
		{
			return $"{prefix}:{attribute.Name.LocalName}";
		}

		var namespaceUri = attribute.Name.Namespace.NamespaceName;
		return string.IsNullOrEmpty(namespaceUri)
			? attribute.Name.LocalName
			: $"{{{namespaceUri}}}{attribute.Name.LocalName}";
	}

	private static void HarvestExtension(XElement node, XElement? extParent, Dictionary<string, string> dict)
	{
		var elemPrefix = node.GetPrefixOfNamespace(node.Name.Namespace);
		var elemQName = FormatQualifiedName(node, elemPrefix);
		var nonNamespaceAttributes = node.Attributes().Where(attr => !attr.IsNamespaceDeclaration).ToList();

		// Zeebe I/O mappings: preserve EVERY <zeebe:output>/<zeebe:input>
		// as a per-target entry (zeebe:ioMapping.output.{target} = source)
		// instead of the generic single {elem}.source/{elem}.target pair,
		// which silently drops all but the last mapping when several exist.
		if ((node.Name.LocalName == "output" || node.Name.LocalName == "input")
			&& node.Name.Namespace?.NamespaceName.Contains("zeebe", StringComparison.OrdinalIgnoreCase) == true)
		{
			var src = node.Attribute("source")?.Value;
			var tgt = node.Attribute("target")?.Value;
			if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(tgt))
			{
				dict[$"zeebe:ioMapping.{node.Name.LocalName}.{tgt}"] = src;
			}
			// fall through: generic harvester also surfaces the (lossy, last-wins)
			// {elem}.source/{elem}.target values for backward compatibility.
		}

		foreach (var attr in nonNamespaceAttributes)
		{
			// Attribute qualified name (attributes can also be namespaced).
			var attrPrefix = attr.Name.Namespace == XNamespace.None
				? null
				: node.GetPrefixOfNamespace(attr.Name.Namespace);
			var attrQName = FormatQualifiedAttributeName(attr, attrPrefix);
			dict[$"{elemQName}.{attrQName}"] = attr.Value;
		}

		if (nonNamespaceAttributes.Count == 0 && node != extParent)
		{
			dict[$"{elemQName}.__present"] = "true";
			if (!node.HasElements && !string.IsNullOrEmpty(node.Value))
			{
				dict[$"{elemQName}.__text"] = node.Value;
			}
		}

		foreach (var child in node.Elements())
		{
			HarvestExtension(child, extParent, dict);
		}
	}


	private List<BpmnMessage> ParseMessages(List<XElement> messages)
	{
		return [.. messages.Select(m => new BpmnMessage(Intern(m.Attribute("id")?.Value ?? string.Empty), m.Attribute("name")?.Value)).Where(m => !string.IsNullOrEmpty(m.Id))];
	}
	private List<BpmnSignal> ParseSignals(List<XElement> signals)
	{
		return [.. signals.Select(s => new BpmnSignal(Intern(s.Attribute("id")?.Value ?? string.Empty), s.Attribute("name")?.Value)).Where(s => !string.IsNullOrEmpty(s.Id))];
	}
	private List<BpmnError> ParseErrors(List<XElement> errors)
	{
		return [.. errors.Select(e => new BpmnError(Intern(e.Attribute("id")?.Value ?? string.Empty), e.Attribute("name")?.Value, e.Attribute("errorCode")?.Value)).Where(e => !string.IsNullOrEmpty(e.Id))];
	}
	private List<BpmnEscalation> ParseEscalations(List<XElement> escalations)
	{
		return [.. escalations.Select(e => new BpmnEscalation(Intern(e.Attribute("id")?.Value ?? string.Empty), e.Attribute("name")?.Value, e.Attribute("escalationCode")?.Value)).Where(e => !string.IsNullOrEmpty(e.Id))];
	}

	/// <summary>
	/// Extrahiert <properties>/<property>-Strukturen aus beliebigen Namespaces (z.B. camunda:properties).
	/// </summary>
	private bool IsLargeModel(XElement? processElement, BpmnParserOptions options)
	{
		if (!options.OptimizeLargeModels || options.LargeModelThreshold <= 0)
		{
			return false;
		}

		var elementCount = processElement?.Elements().Count() ?? 0;

		if (elementCount > options.LargeModelThreshold && options.EnableLogging && _logger.IsEnabled(LogLevel.Debug))
		{
			_logger.LogDebug("Large model detected: ElementCount={ElementCount}, applying optimizations",
				elementCount);
		}

		return elementCount > options.LargeModelThreshold;
	}

	private static List<ValidationDiagnostic> ValidateModel(BpmnModel model)
	{

		var events = model.Events != null ? AsSpanSafe(model.Events) : [];
		var gateways = model.Gateways != null ? AsSpanSafe(model.Gateways) : [];
		var subprocesses = model.Subprocesses != null ? AsSpanSafe(model.Subprocesses) : [];
		var flows = AsSpanSafe(model.SequenceFlows ?? []);
		var tasks = model.Tasks != null ? AsSpanSafe(model.Tasks) : [];
		var legacyDiagnostics = model.Diagnostics != null ? AsSpanSafe(model.Diagnostics) : [];
		var rawLaneElements = model.RawMetadata?.RawLanes; // Null-safe access
		var dataObjects = model.DataObjects != null ? AsSpanSafe(model.DataObjects) : [];
		var dataObjectReferences = model.DataObjectReferences != null ? AsSpanSafe(model.DataObjectReferences) : [];
		var associations = model.Associations != null ? AsSpanSafe(model.Associations) : [];
		var textAnnotations = model.TextAnnotations != null ? AsSpanSafe(model.TextAnnotations) : [];
		var groups = model.Groups != null ? AsSpanSafe(model.Groups) : [];
		var list = new List<ValidationDiagnostic>();
		list.AddRange(ValidateGatewayDefaults(
				model.Gateways ?? [],
				model.SequenceFlows ?? []));

		// ---- Legacy mapping block (keep existing mappings; abbreviated here) ----
		foreach (var msg in legacyDiagnostics)
		{
			MapLegacyValidationMessage(msg, list);
		}

		ValidateFlowElementIds(events, tasks, gateways, subprocesses, flows, list);
		var nodeIds = CollectValidFlowNodeIds(events, tasks, gateways, subprocesses);

		// Sequence flow endpoint & default condition rule
		ValidateSequenceFlowEndpoints(flows, list, nodeIds);

		// LANE FLOW NODE REF rule
		ValidateLaneNodeReferences(rawLaneElements, list, nodeIds);

		// REF-DATAOBJECTREF-TARGET-MISSING
		ValidateDataObjectReferenceTargets(dataObjects, dataObjectReferences, list);

		// REF-ASSOCIATION-ENDPOINT-MISSING (Warning for each missing endpoint)
		ValidateAssociationEndpoints(dataObjects, dataObjectReferences, associations, textAnnotations, groups, list, nodeIds);

		// SEM-EVENTSUBPROCESS-START-TYPE:
		// Validate startEvent definitions inside event subprocesses (triggeredByEvent="true").
		ValidateEventSubprocessStarts(events, subprocesses, list);
		// SEM-EVENTGW-INVALID-OUTGOING:
		// For each event-based gateway, all outgoing targets must be catching intermediate events (intermediateCatchEvent).
		ValidateEventGatewayTargets(events, gateways, flows, list);
		// --
		// - Advisory Reachability Rules (BFS from start events) ---
		// Build adjacency once
		ValidateRootReachability(events, gateways, subprocesses, flows, tasks, list, nodeIds);
		VertexBpmnExtensions.Validate(model, list);
		return list;
	}

	private static List<ValidationDiagnostic> ValidateGatewayDefaults(
	IReadOnlyList<BpmnGateway> gateways,
	IReadOnlyList<BpmnSequenceFlow> flows)
	{
		var result = new List<ValidationDiagnostic>();

		var flowsById = flows
			.Where(flow => !string.IsNullOrWhiteSpace(flow.Id))
			.GroupBy(flow => flow.Id, StringComparer.Ordinal)
			.ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

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

			ValidateResolvedGatewayDefault(gateway, defaultFlow, result);
		}

		return result;
	}
	private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? ParseVendorExtensions(bool strict, Dictionary<string, XElement>? rawExtensions)
	{
		// vendor extension normalization capture (expanded all vendors + generics)
		IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? vendorNormalized = null;
		if (strict && _options.NormalizeVendorExtensions && rawExtensions is { Count: > 0 })
		{


			var tmp = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

			foreach (var kv in rawExtensions)
			{
				var ownerId = kv.Key;
				if (string.IsNullOrEmpty(ownerId))
				{
					continue;
				}

				var root0 = kv.Value;
				var bucket = new Dictionary<string, string>(StringComparer.Ordinal);

				foreach (var child in root0.Elements())
				{
					NormalizeVendorExtensionChild(child, bucket);
				}

				if (bucket.Count > 0)
				{
					tmp[ownerId] = bucket;
				}
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

	private static (LoopCharacteristics? loop, bool conflict) ParseLoopLocal(XElement sp, XNamespace ns, HashSet<string> pendingMiConflicts)
	{
		var res = ParseLoopWithConflict(sp, ns);
		if (res.conflict)
		{
			pendingMiConflicts.Add(sp.Attribute("id")?.Value ?? "");
		}

		return res;
	}

	private static (LoopCharacteristics? loop, bool conflict) ParseLoopWithConflict(XElement sp, XNamespace ns)
	{
		var mi = sp.Element(ns + "multiInstanceLoopCharacteristics") ?? sp.Element("multiInstanceLoopCharacteristics");
		if (mi != null)
		{
			var isSeq = mi.Attribute("isSequential")?.Value == "true";
			int? card = null;
			var cardText = mi.Element(ns + "loopCardinality")?.Value ?? mi.Element("loopCardinality")?.Value;
			if (int.TryParse(cardText, out var cParsed))
			{
				card = cParsed;
			}

			var camundaCollection = mi.Attribute(XName.Get("collection", BpmnXmlNamespaces.Camunda))?.Value;
			var zeebeCollection = mi.Element(XName.Get("inputCollection", BpmnXmlNamespaces.Zeebe))?.Value;
			var collectionRaw = camundaCollection ?? zeebeCollection;
			var camundaElementVar = mi.Attribute(XName.Get("elementVariable", BpmnXmlNamespaces.Camunda))?.Value;
			var zeebeInputElement = mi.Element(XName.Get("inputElement", BpmnXmlNamespaces.Zeebe))?.Value;
			var zeebeOutputElement = mi.Element(XName.Get("outputElement", BpmnXmlNamespaces.Zeebe))?.Value;
			var elementVar = camundaElementVar ?? zeebeInputElement ?? zeebeOutputElement;
			var completion = mi.Element(ns + "completionCondition")?.Value ?? mi.Element("completionCondition")?.Value;
			var conflict = !string.IsNullOrWhiteSpace(collectionRaw) && card.HasValue;
			if (!string.IsNullOrWhiteSpace(collectionRaw))
			{
				card = null;
			}

			var loop = new MultiInstanceLoopCharacteristics(isSeq, card, collectionRaw, elementVar, completion, zeebeInputElement, zeebeOutputElement);
			return (loop, conflict);
		}
		var std = sp.Element(ns + "standardLoopCharacteristics") ?? sp.Element("standardLoopCharacteristics");
		if (std != null)
		{
			var loopCond = std.Element(ns + "loopCondition")?.Value ?? std.Element("loopCondition")?.Value;
			var testBefore = std.Attribute("testBefore")?.Value == "true";
			int? loopMax = null;
			if (int.TryParse(std.Attribute("loopMaximum")?.Value, out var lm))
			{
				loopMax = lm;
			}

			return (new StandardLoopCharacteristics(loopCond, testBefore, loopMax), false);
		}
		return (null, false);
	}


	private static void MaybeThrowOnValidation(BpmnParserOptions options, IReadOnlyList<ValidationDiagnostic> diagnostics)
	{
		if (!options.ThrowOnFatalValidation)
		{
			return;
		}

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
		if (collection is List<T> list)
		{
			return CollectionsMarshal.AsSpan(list);
		}
		if (collection is T[] array)
		{
			return array.AsSpan();
		}
		// An explicitly array-targeted expression keeps the fallback on the heap.
		T[] fallback = [.. collection];
		return fallback.AsSpan();
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
				else if ((!isInStandardNamespace) && options.ValidateEventDefinitions && options.EnableAdvancedValidation)
				{
					// Get the namespace prefix for the event definition element
					var prefix = defElem.GetPrefixOfNamespace(defElem.Name.Namespace);
					var elementDisplayName = string.IsNullOrEmpty(prefix) ? localName : $"{prefix}:{localName}";
					diagnostics.Add(new ValidationDiagnostic(Code: "VEN-UNKNOWN-EVENT-DEFINITION", Severity: ValidationSeverity.Info, Message: $"Event '{eventId}' contains unknown event definition '{elementDisplayName}' (preserved in raw form)", ElementId: eventId, Category: "Vendor"));
				}
			}
			else if (!isInStandardNamespace && defElem.Name.Namespace != XNamespace.None && options.ValidateEventDefinitions && options.EnableAdvancedValidation)
			{
				// Get the namespace prefix for the vendor element
				var prefix = defElem.GetPrefixOfNamespace(defElem.Name.Namespace);
				var elementDisplayName = string.IsNullOrEmpty(prefix) ? localName : $"{prefix}:{localName}";
				diagnostics.Add(new ValidationDiagnostic(Code: "VEN-UNKNOWN-EVENT-DEFINITION", Severity: ValidationSeverity.Info, Message: $"Event '{eventId}' contains unknown event definition '{elementDisplayName}' (preserved in raw form)", ElementId: eventId, Category: "Vendor"));
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
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Sonar Code Smell", "S2325", Justification = "Keep the existing public parser instance API; converting it to static would break consumer call sites.")]
	public string ComputeStructuralModelHash(BpmnModel model)
	{
		ArgumentNullException.ThrowIfNull(model);

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

		AppendEventsStructure(sb, model);
		AppendTasksStructure(sb, model);
		AppendGatewaysStructure(sb, model);
		AppendSubprocessesStructure(sb, model);
		AppendFlowsStructure(sb, model);
		AppendGlobalsStructure(sb, model);

	}

	private static void AppendEventDefinitionStructure(StringBuilder sb, EventDefinition def)
	{
		switch (def)
		{
			case TimerEventDefinition timer:
				AppendHashedFingerprintField(sb, "TD:", timer.TimeDate);
				AppendHashedFingerprintField(sb, "DUR:", timer.TimeDuration);
				AppendHashedFingerprintField(sb, "CYC:", timer.TimeCycle);
				break;
			case MessageEventDefinition message:
				AppendFingerprintField(sb, "REF:", message.MessageRef);
				AppendFingerprintField(sb, "CORR:", message.CorrelationKey);
				break;
			case SignalEventDefinition signal:
				AppendFingerprintField(sb, "REF:", signal.SignalRef);
				break;
			case ErrorEventDefinition error:
				AppendFingerprintField(sb, "REF:", error.ErrorRef);
				break;
			case ConditionalEventDefinition conditional:
				AppendHashedFingerprintField(sb, "COND:", conditional.Condition);
				break;
			case EscalationEventDefinition escalation:
				AppendFingerprintField(sb, "REF:", escalation.EscalationRef);
				break;
			case LinkEventDefinition link:
				AppendFingerprintField(sb, "NAME:", link.Name);
				break;
			case CompensationEventDefinition compensation:
				AppendFingerprintField(sb, "ACT:", compensation.ActivityRef);
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

		foreach (var task in model.Tasks ?? [])
		{
			// Access raw extension elements from the model's raw metadata

			if (rawExtensions?.Count > 0)
			{
				var additionalAttributes = new Dictionary<string, string>(StringComparer.Ordinal);

				foreach (var handler in _options.VendorExtensionHandlers)
				{
					foreach (var extensionElement in rawExtensions.Values)
					{
						foreach (var childElement in extensionElement.Elements().Where(childElement => handler.CanHandle(childElement.Name.NamespaceName, childElement.Name.LocalName)))
						{
							var result = handler.ProcessExtension(childElement, task.Id);
							foreach (var attr in result.NormalizedAttributes)
							{
								additionalAttributes[attr.Key] = attr.Value;
							}
						}
					}
				}

				if (additionalAttributes.Count > 0)
				{
					var combinedAttributes = new Dictionary<string, string>(StringComparer.Ordinal);
					if (task.Attributes != null)
					{
						foreach (var attr in task.Attributes)
						{
							combinedAttributes[attr.Key] = attr.Value;
						}
					}
					foreach (var attr in additionalAttributes)
					{
						combinedAttributes[attr.Key] = attr.Value;
					}

					updatedTasks.Add(task with { Attributes = combinedAttributes });
				}
			}

			updatedTasks.Add(task);
		}

		return updatedTasks;
	}

	private string Intern(string s)
	{
		if (!_options.InternIds)
		{
			return s;
		}

		if (string.IsNullOrEmpty(s))
		{
			return s;
		}

		// Use SharedStringAtomTable for cross-parser memory efficiency
		return SharedStringAtomTable.Intern(s);
	}


}
