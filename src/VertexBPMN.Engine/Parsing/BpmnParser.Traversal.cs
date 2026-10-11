using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Parsing;

public partial class BpmnParser
{
	private sealed class TraversalState
	{
		public required int OrderCounter { get; set; }
		public required Stack<string> SubprocessStack { get; set; }
		public required HashSet<string> IdIndex { get; set; }
		public required List<string> Diagnostics { get; set; }
		public required XNamespace Ns { get; set; }
		public required bool Strict { get; set; }
		public required Dictionary<string, XElement>? RawExtensions { get; set; }
		public required HashSet<string> PendingMiConflicts { get; set; }
		public required Dictionary<string, XElement>? RawMultiInstance { get; set; }
		public required List<BpmnSubprocess> Subprocesses { get; set; }
		public required string CurrentProcessId { get; set; }
		public required HashSet<string> TransactionIds { get; set; }
		public required HashSet<string> FlowNodeIds { get; set; }
		public required Dictionary<string, IReadOnlyDictionary<string, string>>? FlowNodeAttributes { get; set; }
		public required Dictionary<string, List<XElement>>? RawDocumentation { get; set; }
		public required Dictionary<string, ElementMetadata>? ElementsMetadata { get; set; }
		public required List<ValidationDiagnostic> UnknownEventDefinitionDiagnostics { get; set; }
		public required List<BpmnEvent> Events { get; set; }
		public required List<(string Id, string? Attached)> BoundaryEvents { get; set; }
		public required Dictionary<string, int> LinkThrowCounts { get; set; }
		public required HashSet<string> LinkCatchNames { get; set; }
		public required Dictionary<string, List<XElement>>? RawEvDefs { get; set; }
		public required Dictionary<string, (string? Format, string? Body, string? Result)> ScriptTaskRaw { get; set; }
		public required Dictionary<string, string> PotentialOwnerExtras { get; set; }
		public required List<BpmnTask> Tasks { get; set; }
		public required List<BpmnActivityIo> ActivityIo { get; set; }
		public required List<BpmnGateway> Gateways { get; set; }
		public required List<GatewayTraversalInfo> GatewaysRaw { get; set; }
		public required List<XElement>? RawLanes { get; set; }
		public required Dictionary<string, string>? PriorityAttrNs { get; set; }
		public required XElement Root { get; set; }
		public required List<BpmnSequenceFlow> Flows { get; set; }
		public required HashSet<string> DefaultIds { get; set; }
		public required Dictionary<string, (string Raw, bool WasCData)>? RawCond { get; set; }
		public required List<BpmnDataObject> DataObjects { get; set; }
		public required List<BpmnDataObjectReference> DataObjectRefs { get; set; }
		public required List<BpmnDataStore> DataStores { get; set; }
		public required List<BpmnDataStoreReference> DataStoreRefs { get; set; }
		public required List<BpmnProperty> Properties { get; set; }
		public required List<BpmnTextAnnotation> TextAnnotations { get; set; }
		public required List<XElement>? RawArtifacts { get; set; }
		public required List<BpmnGroup> Groups { get; set; }
		public required List<BpmnAssociation> AssociationArtifacts { get; set; }
		public required CancellationToken CancellationToken { get; init; }
	}

	private void Walk(XElement parent, TraversalState state)
	{
		foreach (var el in parent.Elements())
		{
			state.CancellationToken.ThrowIfCancellationRequested();
			state.OrderCounter++;
			var local = el.Name.LocalName;
			var id = Intern(el.Attribute("id")?.Value ?? string.Empty);
			var currentSub = state.SubprocessStack.Count > 0 ? state.SubprocessStack.Peek() : null;
			if (!string.IsNullOrEmpty(id) && !state.IdIndex.Add(id) && _options.StrictValidation)
			{
				state.Diagnostics.Add($"Duplicate ID: {id}");
			}

			var ext = ExtractExtensions(el, state.Ns, state.Strict, state.RawExtensions);
			switch (local)
			{
				case "subProcess":
				case "adHocSubProcess":
				case "transaction":
					TraverseSubprocess(el, local, id, currentSub, ext, state);
					break;
				case "startEvent":
				case "endEvent":
				case "intermediateCatchEvent":
				case "intermediateThrowEvent":
				case "boundaryEvent":
					TraverseEvent(el, local, id, currentSub, ext, state);
					break;
				case var _ when local == "task" || local.EndsWith("Task", StringComparison.Ordinal) || local == "callActivity":
					TraverseTask(el, local, id, currentSub, ext, state);
					break;
				case var _ when local.EndsWith("Gateway", StringComparison.Ordinal):
					TraverseGateway(el, local, id, currentSub, ext, state);
					break;
				case "laneSet":
					TraverseLaneSet(el, state);
					continue; // Lane sets recurse and skip flow-node diagnostics.
				case "sequenceFlow":
					TraverseSequenceFlow(el, id, currentSub, ext, state);
					break;
				default:
					TraverseArtifactElement(el, local, id, state);
					break;
			}

			// Only flow nodes and sequence flows require an ID for these diagnostics.
			if (state.Strict && string.IsNullOrEmpty(id)
				&& (local is "userTask" or "serviceTask" or "task"
					|| local.EndsWith("Task", StringComparison.Ordinal)
					|| local is "startEvent" or "endEvent" or "intermediateCatchEvent" or "intermediateThrowEvent" or "boundaryEvent"
					|| local.EndsWith("Gateway", StringComparison.Ordinal) || local is "sequenceFlow"))
			{
				state.Diagnostics.Add($"Missing id on {local}");
			}
		}
	}

	private void TraverseArtifactElement(XElement el, string local, string id, TraversalState state)
	{
		switch (local)
		{
			case "dataObject":
				TraverseDataObject(el, id, state);
				break;
			case "dataObjectReference":
				TraverseDataObjectReference(el, id, state);
				break;
			case "dataStore":
				TraverseDataStore(el, id, state);
				break;
			case "dataStoreReference":
				TraverseDataStoreReference(el, id, state);
				break;
			case "property":
				TraverseProperty(el, id, state);
				break;
			case "textAnnotation":
				TraverseTextAnnotation(el, id, state);
				break;
			case "group":
				TraverseGroup(el, id, state);
				break;
			case "association":
				TraverseAssociation(el, id, state);
				break;
			case "lane":
				TraverseLane(el, id, state);
				break;
		}
	}

	private void TraverseSubprocess(XElement el, string local, string id, string? currentSub, Dictionary<string, string>? ext, TraversalState state)
	{
		var isEvent = el.Attribute("triggeredByEvent")?.Value == "true";
		var isTx = el.Attribute("transaction")?.Value == "true" || local == "transaction";
		if (el.Attribute("isForCompensation")?.Value is { } subprocessCompensation)
		{
			ext ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			ext["isForCompensation"] = subprocessCompensation;
		}

		if (el.Attribute("name")?.Value is { } subprocessName)
		{
			ext ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			ext["name"] = subprocessName;
		}

		var (loop, _) = ParseLoopLocal(el, state.Ns, state.PendingMiConflicts); // capture raw loop node
		if (state.Strict && !string.IsNullOrEmpty(id))
		{
			var miNode = el.Element(state.Ns + "multiInstanceLoopCharacteristics") ?? el.Element("multiInstanceLoopCharacteristics");
			var stdNode = el.Element(state.Ns + "standardLoopCharacteristics") ?? el.Element("standardLoopCharacteristics");
			var rawLoop = miNode ?? stdNode;
			if (rawLoop is not null)
			{
				state.RawMultiInstance![id] = new XElement(rawLoop);
			}
		}

		state.Subprocesses.Add(new BpmnSubprocess(id, isEvent, isTx, loop, currentSub, ext) { ProcessId = state.CurrentProcessId });
		if (isTx && !string.IsNullOrEmpty(id))
		{
			state.TransactionIds.Add(id);
		}

		state.FlowNodeIds.Add(id);
		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id, el.Attribute(XName.Get("collection", BpmnXmlNamespaces.Camunda)) != null, el.Element(XName.Get("inputCollection", BpmnXmlNamespaces.Zeebe)) != null, el.Element(state.Ns + "loopCardinality") != null || el.Element("loopCardinality") != null, el.Attribute(XName.Get("elementVariable", BpmnXmlNamespaces.Camunda)) != null, el.Element(XName.Get("inputElement", BpmnXmlNamespaces.Zeebe)) != null, el.Element(XName.Get("outputElement", BpmnXmlNamespaces.Zeebe)) != null);
		state.SubprocessStack.Push(id);
		Walk(el, state);
		state.SubprocessStack.Pop();
	}

	private void TraverseEvent(XElement el, string local, string id, string? currentSub, Dictionary<string, string>? ext, TraversalState state)
	{
		var (defs, eventDefDiagnostics) = ParseEventDefinitionsWithDiagnostics(el, state.Ns, _options);
		state.UnknownEventDefinitionDiagnostics.AddRange(eventDefDiagnostics);
		var eventAttributes = BuildEventAttributes(el, ext);
		state.Events.Add(new BpmnEvent(id, local, defs, currentSub, eventAttributes) { ProcessId = state.CurrentProcessId });
		state.FlowNodeIds.Add(id);
		if (local == "boundaryEvent")
		{
			state.BoundaryEvents.Add((id, el.Attribute("attachedToRef")?.Value));
		}

		// link events tracking
		foreach (var led in el.Elements())
		{
			if (led.Name.LocalName == "linkEventDefinition")
			{
				var lname = led.Attribute("name")?.Value;
				if (!string.IsNullOrEmpty(lname))
				{
					if (local == "intermediateThrowEvent")
					{
						state.LinkThrowCounts.TryGetValue(lname, out var c);
						state.LinkThrowCounts[lname] = c + 1;
					}
					else if (local == "intermediateCatchEvent")
					{
						state.LinkCatchNames.Add(lname);
					}
				}
			}
		}

		if (state.Strict && _options.CaptureRawEventDefinitions)
		{
			var list = new List<XElement>();
			foreach (var d in el.Elements())
			{
				// Capture standard BPMN event definitions OR vendor/unknown elements
				if (d.Name.LocalName.EndsWith("EventDefinition", StringComparison.OrdinalIgnoreCase) || d.Name.LocalName.Contains("EventDefinition", StringComparison.OrdinalIgnoreCase) || (d.Name.Namespace != state.Ns && d.Name.Namespace != XNamespace.None))
				{
					list.Add(new XElement(d));
				}
			}

			if (list.Count > 0)
			{
				state.RawEvDefs![id] = list;
			}
		}

		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private void TraverseTask(XElement el, string local, string id, string? currentSub, Dictionary<string, string>? ext, TraversalState state)
	{
		if (state.Strict && !string.IsNullOrEmpty(id))
		{
			var miNodeT = el.Element(state.Ns + "multiInstanceLoopCharacteristics") ?? el.Element("multiInstanceLoopCharacteristics");
			var stdNodeT = el.Element(state.Ns + "standardLoopCharacteristics") ?? el.Element("standardLoopCharacteristics");
			var rawTaskLoop = miNodeT ?? stdNodeT;
			if (rawTaskLoop is not null)
			{
				state.RawMultiInstance![id] = new XElement(rawTaskLoop);
			}
		}

		if (local == "scriptTask" && !string.IsNullOrEmpty(id))
		{
			var fmt = el.Attribute("scriptFormat")?.Value;
			var body = el.Element(state.Ns + "script")?.Value ?? el.Element("script")?.Value;
			var resVar = el.Attribute("resultVariable")?.Value;
			state.ScriptTaskRaw[id] = (fmt, body, resVar);
		}

		if (local == "userTask" && !string.IsNullOrEmpty(id))
		{
			var roles = el.Elements().Where(e => e.Name.LocalName == "potentialOwner" || (e.Name.LocalName == "resourceRole" && (string?)e.Attribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance")) == "potentialOwner"));
			foreach (var role in roles)
			{
				var formal = role.Element(state.Ns + "resourceAssignmentExpression") ?? role.Element("resourceAssignmentExpression");
				var expr = formal?.Element(state.Ns + "formalExpression") ?? formal?.Element("formalExpression");
				var text = expr?.Value?.Trim();
				if (!string.IsNullOrWhiteSpace(text))
				{
					state.PotentialOwnerExtras[id] = text;
				}
			}
		}

		var taskAttributes = BuildTaskAttributes(el, state.Ns, ext);
		var implementation = el.Attribute("implementation")?.Value;
		if (local == "serviceTask" && taskAttributes?.ContainsKey("vertex:connector.type") == true)
		{
			implementation = "vertex:connector";
		}

		var task = new BpmnTask(id, local, currentSub, taskAttributes, implementation)
		{
			Loop = ParseLoopLocal(el, state.Ns, state.PendingMiConflicts).loop,
			Name = el.Attribute("name")?.Value ?? string.Empty,
			ProcessId = state.CurrentProcessId
		};
		if (state.Tasks.All(x => x.Id != id))
		{
			state.Tasks.Add(task);
		}

		state.FlowNodeIds.Add(id);
		CaptureTaskIo(el, id, state);
		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private void CaptureTaskIo(XElement el, string id, TraversalState state)
	{
		// IO spec & associations
		var ioSpec = el.Element(state.Ns + "ioSpecification") ?? el.Element("ioSpecification");
		if (ioSpec != null)
		{
			var dataInputs = ioSpec.Elements(state.Ns + "dataInput").Concat(ioSpec.Elements("dataInput")).Select(di =>
			{
				var did = Intern(di.Attribute("id")?.Value ?? string.Empty);
				if (string.IsNullOrEmpty(did))
				{
					return null;
				}

				return new BpmnDataInput(did, di.Attribute("name")?.Value);
			}).OfType<BpmnDataInput>().ToList();
			var dataOutputs = ioSpec.Elements(state.Ns + "dataOutput").Concat(ioSpec.Elements("dataOutput")).Select(dout =>
			{
				var oid = Intern(dout.Attribute("id")?.Value ?? string.Empty);
				if (string.IsNullOrEmpty(oid))
				{
					return null;
				}

				return new BpmnDataOutput(oid, dout.Attribute("name")?.Value);
			}).OfType<BpmnDataOutput>().ToList();
			var inputAssociations = el.Elements(state.Ns + "dataInputAssociation").Concat(el.Elements("dataInputAssociation")).Select(a =>
			{
				var src = a.Element(state.Ns + "sourceRef")?.Value ?? a.Element("sourceRef")?.Value;
				var tgt = a.Element(state.Ns + "targetRef")?.Value ?? a.Element("targetRef")?.Value;
				if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(tgt))
				{
					return null;
				}

				return new BpmnDataAssociation(Intern(src), Intern(tgt));
			}).OfType<BpmnDataAssociation>().ToList();
			var outputAssociations = el.Elements(state.Ns + "dataOutputAssociation").Concat(el.Elements("dataOutputAssociation")).Select(a =>
			{
				var src = a.Element(state.Ns + "sourceRef")?.Value ?? a.Element("sourceRef")?.Value;
				var tgt = a.Element(state.Ns + "targetRef")?.Value ?? a.Element("targetRef")?.Value;
				if (string.IsNullOrWhiteSpace(src) || string.IsNullOrWhiteSpace(tgt))
				{
					return null;
				}

				return new BpmnDataAssociation(Intern(src), Intern(tgt));
			}).OfType<BpmnDataAssociation>().ToList();
			if (dataInputs.Count > 0 || dataOutputs.Count > 0 || inputAssociations.Count > 0 || outputAssociations.Count > 0)
			{
				state.ActivityIo.Add(new BpmnActivityIo(id, dataInputs, dataOutputs, inputAssociations, outputAssociations));
			}
		}
	}

	private void TraverseGateway(XElement el, string local, string id, string? currentSub, Dictionary<string, string>? ext, TraversalState state)
	{
		if (el.Attribute("name")?.Value is { } gatewayName)
		{
			ext ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			ext["name"] = gatewayName;
		}

		var activationCondition = (el.Element(state.Ns + "activationCondition") ?? el.Element("activationCondition"))?.Value.Trim();
		if (!string.IsNullOrWhiteSpace(activationCondition))
		{
			ext ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			ext["activationCondition"] = activationCondition;
		}

		state.FlowNodeIds.Add(id);
		state.Gateways.Add(new BpmnGateway(id, local, state.GatewaysRaw.FirstOrDefault(g => g.Id == id)?.DefaultId, currentSub, ext) { ProcessId = state.CurrentProcessId });
		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private void TraverseLaneSet(XElement el, TraversalState state)
	{
		if (state.Strict)
		{
			state.RawLanes!.Add(new XElement(el)); // keep laneSet structure
		}

		Walk(el, state); // recurse into lanes
	}

	private void TraverseSequenceFlow(XElement el, string id, string? currentSub, Dictionary<string, string>? ext, TraversalState state)
	{
		int? priority = null;
		var prAttr = el.Attribute(XName.Get("priority", BpmnXmlNamespaces.Vertex)) ?? el.Attribute(XName.Get("priority", BpmnXmlNamespaces.Camunda)) ?? el.Attribute("priority");
		if (prAttr != null && int.TryParse(prAttr.Value, out var pVal))
		{
			priority = pVal;
		}

		if (state.Strict && prAttr != null && !string.IsNullOrEmpty(id))
		{
			state.PriorityAttrNs![id] = prAttr.Name.NamespaceName;
		}

		var condNode = el.Element(state.Ns + "conditionExpression") ?? el.Element("conditionExpression");
		var condText = condNode?.Value?.Trim();
		if (!string.IsNullOrWhiteSpace(condText))
		{
			ext ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			ext["conditionExpression"] = condText;
			var expressionLanguage = condNode?.Attribute("language")?.Value ?? state.Root.Attribute("expressionLanguage")?.Value;
			if (!string.IsNullOrWhiteSpace(expressionLanguage))
			{
				ext["conditionExpressionLanguage"] = expressionLanguage;
			}
		}

		state.Flows.Add(new BpmnSequenceFlow(id, Intern(el.Attribute("sourceRef")?.Value ?? string.Empty), Intern(el.Attribute("targetRef")?.Value ?? string.Empty), state.DefaultIds.Contains(id), condText, currentSub, ext, priority) { ProcessId = state.CurrentProcessId });
		if (state.Strict && condNode != null)
		{
			var wasCData = condNode.Nodes().OfType<XCData>().Any();
			state.RawCond![id] = (condNode.Value, wasCData);
		}

		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private static void TraverseDataObject(XElement el, string id, TraversalState state)
	{
		state.DataObjects.Add(new BpmnDataObject(id, el.Attribute("name")?.Value));
		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private void TraverseDataObjectReference(XElement el, string id, TraversalState state)
	{
		state.DataObjectRefs.Add(new BpmnDataObjectReference(id, Intern(el.Attribute("dataObjectRef")?.Value ?? string.Empty)));
		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private static void TraverseDataStore(XElement el, string id, TraversalState state)
	{
		state.DataStores.Add(new BpmnDataStore(id, el.Attribute("name")?.Value));
		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private void TraverseDataStoreReference(XElement el, string id, TraversalState state)
	{
		state.DataStoreRefs.Add(new BpmnDataStoreReference(id, Intern(el.Attribute("dataStoreRef")?.Value ?? string.Empty)));
		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private static void TraverseProperty(XElement el, string id, TraversalState state)
	{
		state.Properties.Add(new BpmnProperty(id, el.Attribute("name")?.Value));
		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private void TraverseTextAnnotation(XElement el, string id, TraversalState state)
	{
		if (!_options.CaptureArtifacts && state.Strict)
		{
			CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
			return;
		}

		state.TextAnnotations.Add(new BpmnTextAnnotation(id, el.Element(state.Ns + "text")?.Value ?? el.Element("text")?.Value));
		if (state.Strict)
		{
			state.RawArtifacts?.Add(new XElement(el));
		}

		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private void TraverseGroup(XElement el, string id, TraversalState state)
	{
		if (!_options.CaptureArtifacts && state.Strict)
		{
			CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
			return;
		}

		state.Groups.Add(new BpmnGroup(id, el.Attribute("categoryValueRef")?.Value));
		if (state.Strict)
		{
			state.RawArtifacts?.Add(new XElement(el));
		}

		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private void TraverseAssociation(XElement el, string id, TraversalState state)
	{
		if (!_options.CaptureArtifacts && state.Strict)
		{
			CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
			return;
		}

		state.AssociationArtifacts.Add(new BpmnAssociation(id, el.Attribute("sourceRef")?.Value ?? string.Empty, el.Attribute("targetRef")?.Value ?? string.Empty, el.Attribute("associationDirection")?.Value));
		if (state.Strict)
		{
			state.RawArtifacts?.Add(new XElement(el));
		}

		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}

	private static void TraverseLane(XElement el, string id, TraversalState state)
	{
		if (state.Strict)
		{
			state.RawLanes!.Add(new XElement(el));
		}

		CaptureElementMeta(state.Strict, state.FlowNodeAttributes, state.RawDocumentation, state.ElementsMetadata, state.OrderCounter, state.Ns, el, id);
	}
}
