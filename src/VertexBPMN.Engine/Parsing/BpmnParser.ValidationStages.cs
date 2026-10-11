using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Parsing;

public partial class BpmnParser
{
	private static void ValidateResolvedGatewayDefault(BpmnGateway gateway, BpmnSequenceFlow defaultFlow, List<ValidationDiagnostic> result)
	{
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

	private static void ValidateFlowElementIds(ReadOnlySpan<BpmnEvent> events, ReadOnlySpan<BpmnTask> tasks, ReadOnlySpan<BpmnGateway> gateways, ReadOnlySpan<BpmnSubprocess> subprocesses, ReadOnlySpan<BpmnSequenceFlow> flows, List<ValidationDiagnostic> list)
	{


		// NEW: Direct missing ID validation (not just legacy conversion)
		// Check for missing IDs on elements that require them
		var elementsRequiringIds = new List<(string? Id, string Type)>();

		// Collect all elements that should have IDs
		foreach (var ev in events)
		{
			elementsRequiringIds.Add((ev.Id, ev.Type));
		}

		foreach (var task in tasks)
		{
			elementsRequiringIds.Add((task.Id, task.Type));
		}

		foreach (var gw in gateways)
		{
			elementsRequiringIds.Add((gw.Id, gw.Type));
		}

		foreach (var sp in subprocesses)
		{
			elementsRequiringIds.Add((sp.Id, "subProcess"));
		}

		foreach (var flow in flows)
		{
			elementsRequiringIds.Add((flow.Id, "sequenceFlow"));
		}

		// Generate STR-MISSING-ID diagnostics for elements without IDs
		ValidateRequiredElementIds(list, elementsRequiringIds);


	}

	private static HashSet<string> CollectValidFlowNodeIds(ReadOnlySpan<BpmnEvent> events, ReadOnlySpan<BpmnTask> tasks, ReadOnlySpan<BpmnGateway> gateways, ReadOnlySpan<BpmnSubprocess> subprocesses)
	{
		// Build set of valid flow node ids
		var nodeIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var e in events)
		{
			if (!string.IsNullOrEmpty(e.Id))
			{
				nodeIds.Add(e.Id);
			}
		}

		foreach (var t in tasks)
		{
			if (!string.IsNullOrEmpty(t.Id))
			{
				nodeIds.Add(t.Id);
			}
		}

		foreach (var g in gateways)
		{
			if (!string.IsNullOrEmpty(g.Id))
			{
				nodeIds.Add(g.Id);
			}
		}

		foreach (var sp in subprocesses)
		{
			if (!string.IsNullOrEmpty(sp.Id))
			{
				nodeIds.Add(sp.Id);
			}
		}


		return nodeIds;
	}

	private static void MapLegacyIdentifierDiagnostics(string msg, List<ValidationDiagnostic> list)
	{
		if (msg.StartsWith("Duplicate ID:", StringComparison.Ordinal))
		{
			var id = msg["Duplicate ID:".Length..].Trim();
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
			var id = msg[miPrefix.Length..].Trim();
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
	}

	private static void MapLegacyReferenceDiagnostics(string msg, List<ValidationDiagnostic> list)
	{
		if (msg.StartsWith("boundaryEvent ", StringComparison.Ordinal) &&
				msg.EndsWith(" missing", StringComparison.Ordinal) &&
				msg.Contains(" attachedToRef ", StringComparison.Ordinal))
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
		if (msg.StartsWith("Unknown messageRef '", StringComparison.Ordinal))
		{
			if (TryExtractLegacyReference(msg, "Unknown messageRef '", "' at event ", out var refId, out var evId))
			{
				AddLegacyReferenceDiagnostic(list, "REF-GLOBAL-MESSAGE-MISSING", evId, $"Event '{evId}' references unknown message '{refId}'");
			}
		}
		else if (msg.StartsWith("Unknown signalRef '", StringComparison.Ordinal))
		{
			if (TryExtractLegacyReference(msg, "Unknown signalRef '", "' at event ", out var refId, out var evId))
			{
				AddLegacyReferenceDiagnostic(list, "REF-GLOBAL-SIGNAL-MISSING", evId, $"Event '{evId}' references unknown signal '{refId}'");
			}
		}
		else if (msg.StartsWith("Unknown errorRef '", StringComparison.Ordinal))
		{
			if (TryExtractLegacyReference(msg, "Unknown errorRef '", "' at event ", out var refId, out var evId))
			{
				AddLegacyReferenceDiagnostic(list, "REF-GLOBAL-ERROR-MISSING", evId, $"Event '{evId}' references unknown error '{refId}'");
			}
		}
		else if (msg.StartsWith("Unknown escalationRef '", StringComparison.Ordinal) && TryExtractLegacyReference(msg, "Unknown escalationRef '", "' at event ", out var refId, out var evId))
		{
			AddLegacyReferenceDiagnostic(list, "REF-GLOBAL-ESCALATION-MISSING", evId, $"Event '{evId}' references unknown escalation '{refId}'");
		}
	}

	private static void MapLegacyLinkDiagnostics(string msg, List<ValidationDiagnostic> list)
	{
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
	}

	private static void MapLegacyEventScopeDiagnostics(string msg, List<ValidationDiagnostic> list)
	{
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
	}

	private static void MapLegacyValidationMessage(string msg, List<ValidationDiagnostic> list)
	{
		MapLegacyIdentifierDiagnostics(msg, list);
		MapLegacyReferenceDiagnostics(msg, list);
		MapLegacyLinkDiagnostics(msg, list);
		MapLegacyEventScopeDiagnostics(msg, list);
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
			return;
		}
		if (msg.StartsWith("Missing id on ", StringComparison.Ordinal))
		{
			var type = msg["Missing id on ".Length..].Trim();
			if (type.Length == 0)
			{
				type = "element";
			}
			// Allow multiple occurrences (could be several elements)
			list.Add(new ValidationDiagnostic(
				Code: "STR-MISSING-ID",
				Severity: ValidationSeverity.Error,
				Message: $"Flow node of type '{type}' is missing a required id",
				ElementId: null,
				Category: "Structural"));
		}
	}

	private static bool TryExtractLegacyReference(string msg, string prefix, string mid, out string refId, out string eventId)
	{
		refId = string.Empty;
		eventId = string.Empty;
		if (!msg.StartsWith(prefix, StringComparison.Ordinal))
		{
			return false;
		}

		var midIdx = msg.IndexOf(mid, StringComparison.Ordinal);
		if (midIdx < 0)
		{
			return false;
		}

		refId = msg[prefix.Length..midIdx];
		eventId = msg[(midIdx + mid.Length)..].Trim();
		return refId.Length > 0 && eventId.Length > 0;
	}

	private static void AddLegacyReferenceDiagnostic(List<ValidationDiagnostic> list, string code, string elementId, string message)
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

	private static void ValidateRootReachability(ReadOnlySpan<BpmnEvent> events, ReadOnlySpan<BpmnGateway> gateways, ReadOnlySpan<BpmnSubprocess> subprocesses, ReadOnlySpan<BpmnSequenceFlow> flows, ReadOnlySpan<BpmnTask> tasks, List<ValidationDiagnostic> list, HashSet<string> nodeIds)
	{
		if (events.Length == 0 && tasks.Length == 0 && gateways.Length == 0 && subprocesses.Length == 0)
		{
			return;
		}
		var reachable = FindRootReachableNodes(events, flows);
		if (reachable.Count > 0)
		{
			ReportReachabilityDiagnostics(events, flows, list, nodeIds, reachable);
		}
	}

	private static HashSet<string> FindRootReachableNodes(ReadOnlySpan<BpmnEvent> events, ReadOnlySpan<BpmnSequenceFlow> flows)
	{
		var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (var f in flows)
		{
			if (string.IsNullOrEmpty(f.SourceRef) || string.IsNullOrEmpty(f.TargetRef))
			{
				continue;
			}

			if (!adjacency.TryGetValue(f.SourceRef, out var listTargets))
			{
				listTargets = [];
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

		foreach (var s in startIds.Where(s => reachable.Add(s)))
		{
			queue.Enqueue(s);
		}

		while (queue.Count > 0)
		{
			var cur = queue.Dequeue();
			if (adjacency.TryGetValue(cur, out var outs))
			{
				foreach (var tgt in outs.Where(tgt => !string.IsNullOrEmpty(tgt) && reachable.Add(tgt)))
				{
					queue.Enqueue(tgt);
				}
			}
		}


		return reachable;
	}

	private static void ReportReachabilityDiagnostics(ReadOnlySpan<BpmnEvent> events, ReadOnlySpan<BpmnSequenceFlow> flows, List<ValidationDiagnostic> list, HashSet<string> nodeIds, HashSet<string> reachable)
	{
		foreach (var nid in nodeIds)
		{
			if ((!reachable.Contains(nid)) && (!list.Exists(d => d.Code == "ADV-UNREACHABLE-NODE" && d.ElementId == nid)))
			{
				list.Add(new ValidationDiagnostic(Code: "ADV-UNREACHABLE-NODE", Severity: ValidationSeverity.Info, Message: $"Flow node '{nid}' is unreachable from any start event", ElementId: nid, Category: "Advisory"));
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
			if (string.IsNullOrEmpty(f.Id))
			{
				continue;
			}

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

	private static void ValidateEventGatewayTargets(ReadOnlySpan<BpmnEvent> events, ReadOnlySpan<BpmnGateway> gateways, ReadOnlySpan<BpmnSequenceFlow> flows, List<ValidationDiagnostic> list)
	{
		if (gateways.Length > 0)
		{
			// Build quick lookup for events by id and type
			var eventTypeById = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (var ev in events)
			{
				if (!string.IsNullOrEmpty(ev.Id))
				{
					eventTypeById[ev.Id] = ev.Type;
				}
			}

			// Group flows by source for efficiency
			var flowsBySource = BuildFlowsBySource(flows);
			// ✅ SPAN-OPTIMIZED: Replace LINQ Where() with direct span iteration
			foreach (var gw in gateways)
			{
				if (!string.Equals(gw.Type, "eventBasedGateway", StringComparison.Ordinal) ||
					string.IsNullOrEmpty(gw.Id))
				{
					continue;
				}

				if (!flowsBySource.TryGetValue(gw.Id, out var outgoing))
				{
					continue;
				}

				foreach (var flow in outgoing)
				{
					var tgt = flow.TargetRef;
					if (string.IsNullOrEmpty(tgt))
					{
						// Endpoint rule already covers missing target; skip duplication
						continue;
					}

					// Valid only if target is an intermediateCatchEvent
					if ((!eventTypeById.TryGetValue(tgt, out var tType) || !string.Equals(tType, "intermediateCatchEvent", StringComparison.Ordinal)) && (!list.Exists(d => d.Code == "SEM-EVENTGW-INVALID-OUTGOING" && d.ElementId == gw.Id && d.Message.Contains(tgt, StringComparison.Ordinal))))
					{
						list.Add(new ValidationDiagnostic(Code: "SEM-EVENTGW-INVALID-OUTGOING", Severity: ValidationSeverity.Error, Message: $"Event-based gateway '{gw.Id}' has outgoing flow '{flow.Id}' to invalid target '{tgt}' (must target intermediateCatchEvent)", ElementId: gw.Id, Category: "Semantic"));
					}
				}
			}
		}
	}

	private static Dictionary<string, List<BpmnSequenceFlow>> BuildFlowsBySource(ReadOnlySpan<BpmnSequenceFlow> flows)
	{
		var flowsBySource = new Dictionary<string, List<BpmnSequenceFlow>>(StringComparer.Ordinal);
		foreach (var f in flows)
		{
			if (string.IsNullOrEmpty(f.SourceRef))
			{
				continue;
			}

			if (!flowsBySource.TryGetValue(f.SourceRef, out var lst))
			{
				lst = [];
				flowsBySource[f.SourceRef] = lst;
			}
			lst.Add(f);
		}


		return flowsBySource;
	}

	private static void ValidateAssociationEndpoints(ReadOnlySpan<BpmnDataObject> dataObjects, ReadOnlySpan<BpmnDataObjectReference> dataObjectReferences, ReadOnlySpan<BpmnAssociation> associations, ReadOnlySpan<BpmnTextAnnotation> textAnnotations, ReadOnlySpan<BpmnGroup> groups, List<ValidationDiagnostic> list, HashSet<string> nodeIds)
	{
		if (associations.Length > 0)
		{
			// Build a set of "known" artifact/flow node ids that associations could legally reference.
			var knownIds = BuildAssociationTargetIds(dataObjects, dataObjectReferences, textAnnotations, groups, nodeIds);
			foreach (var assoc in associations)
			{
				if (string.IsNullOrEmpty(assoc.Id))
				{
					continue;
				}

				var missingSource = string.IsNullOrEmpty(assoc.SourceRef) || !knownIds.Contains(assoc.SourceRef);
				var missingTarget = string.IsNullOrEmpty(assoc.TargetRef) || !knownIds.Contains(assoc.TargetRef);

				if (missingSource && (!list.Exists(d => d.Code == "REF-ASSOCIATION-ENDPOINT-MISSING" && d.ElementId == assoc.Id && d.Message.Contains("sourceRef", StringComparison.OrdinalIgnoreCase))))
				{
					list.Add(new ValidationDiagnostic(Code: "REF-ASSOCIATION-ENDPOINT-MISSING", Severity: ValidationSeverity.Warning, Message: $"Association '{assoc.Id}' sourceRef '{assoc.SourceRef}' not found", ElementId: assoc.Id, Category: "Referential"));
				}
				if (missingTarget && (!list.Exists(d => d.Code == "REF-ASSOCIATION-ENDPOINT-MISSING" && d.ElementId == assoc.Id && d.Message.Contains("targetRef", StringComparison.OrdinalIgnoreCase))))
				{
					list.Add(new ValidationDiagnostic(Code: "REF-ASSOCIATION-ENDPOINT-MISSING", Severity: ValidationSeverity.Warning, Message: $"Association '{assoc.Id}' targetRef '{assoc.TargetRef}' not found", ElementId: assoc.Id, Category: "Referential"));
				}
			}
		}
	}

	private static HashSet<string> BuildAssociationTargetIds(ReadOnlySpan<BpmnDataObject> dataObjects, ReadOnlySpan<BpmnDataObjectReference> dataObjectReferences, ReadOnlySpan<BpmnTextAnnotation> textAnnotations, ReadOnlySpan<BpmnGroup> groups, HashSet<string> nodeIds)
	{
		var knownIds = new HashSet<string>(nodeIds, StringComparer.Ordinal);
		foreach (var d in dataObjects)
		{
			if (!string.IsNullOrEmpty(d.Id))
			{
				knownIds.Add(d.Id);
			}
		}

		foreach (var tn in textAnnotations)
		{
			if (!string.IsNullOrEmpty(tn.Id))
			{
				knownIds.Add(tn.Id);
			}
		}

		foreach (var g in groups)
		{
			if (!string.IsNullOrEmpty(g.Id))
			{
				knownIds.Add(g.Id);
			}
		}

		foreach (var dref in dataObjectReferences)
		{
			if (!string.IsNullOrEmpty(dref.Id))
			{
				knownIds.Add(dref.Id);
			}
		}

		return knownIds;
	}

	private static void ValidateEventSubprocessStarts(ReadOnlySpan<BpmnEvent> events, ReadOnlySpan<BpmnSubprocess> subprocesses, List<ValidationDiagnostic> list)
	{
		if (subprocesses.Length > 0)
		{
			// Allowed event definition CLR types
			// Build event subprocess IDs set using spans - much faster than LINQ
			var eventSubIds = new HashSet<string>(StringComparer.Ordinal);
			foreach (var sp in subprocesses)
			{
				if (sp.IsEventSubprocess && !string.IsNullOrEmpty(sp.Id))
				{
					eventSubIds.Add(sp.Id);
				}
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
					{
						continue;
					}

					if (string.IsNullOrEmpty(ev.Id))
					{
						continue;
					}

					if ((ev.Definitions ?? []).Count == 0)
					{
						AddStartTypeDiagnostic(list, ev, "none");
						continue;
					}

					var invalidDefinition = (ev.Definitions ?? []).FirstOrDefault(def => !IsAllowedEventSubprocessStart(def));
					if (invalidDefinition is not null)
					{
						var badType = invalidDefinition.GetType().Name.Replace("EventDefinition", "", StringComparison.Ordinal);
						AddStartTypeDiagnostic(list, ev, badType);
					}
				}


			}
		}
	}

	private static bool IsAllowedEventSubprocessStart(EventDefinition def) =>
				def is MessageEventDefinition
				 or TimerEventDefinition
				 or EscalationEventDefinition
				 or ErrorEventDefinition
				 or CompensationEventDefinition
				 or SignalEventDefinition
				 or ConditionalEventDefinition;

	private static void AddStartTypeDiagnostic(List<ValidationDiagnostic> list, BpmnEvent e, string problem)
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

	private static void ValidateRequiredElementIds(List<ValidationDiagnostic> list, List<(string? Id, string Type)> elementsRequiringIds)
	{
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
	}

	private static void ValidateSequenceFlowEndpoints(ReadOnlySpan<BpmnSequenceFlow> flows, List<ValidationDiagnostic> list, HashSet<string> nodeIds)
	{
		foreach (var flow in flows)
		{
			if (string.IsNullOrEmpty(flow.Id))
			{
				continue;
			}

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
	}

	private static void ValidateLaneNodeReferences(IReadOnlyList<XElement>? rawLaneElements, List<ValidationDiagnostic> list, HashSet<string> nodeIds)
	{
		if (rawLaneElements is { Count: > 0 })
		{
			foreach (var laneEl in rawLaneElements.Where(x => x.Name.LocalName == "lane"))
			{
				var laneId = laneEl.Attribute("id")?.Value;
				if (string.IsNullOrEmpty(laneId))
				{
					continue;
				}

				foreach (var fnRef in laneEl.Elements().Where(e => e.Name.LocalName == "flowNodeRef"))
				{
					var refId = fnRef.Value?.Trim();
					if (string.IsNullOrEmpty(refId))
					{
						continue;
					}

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
	}

	private static void ValidateDataObjectReferenceTargets(ReadOnlySpan<BpmnDataObject> dataObjects, ReadOnlySpan<BpmnDataObjectReference> dataObjectReferences, List<ValidationDiagnostic> list)
	{
		if (dataObjectReferences.Length > 0)
		{
			var dataObjectIds = new HashSet<string>(dataObjects.Length, StringComparer.Ordinal);
			foreach (var d in dataObjects)
			{
				if (!string.IsNullOrEmpty(d.Id))
				{
					dataObjectIds.Add(d.Id);
				}
			}

			foreach (var dref in dataObjectReferences)
			{
				if (string.IsNullOrEmpty(dref.Id))
				{
					continue;
				}

				if ((string.IsNullOrEmpty(dref.DataObjectRef) || !dataObjectIds.Contains(dref.DataObjectRef)) && (!list.Exists(d => d.Code == "REF-DATAOBJECTREF-TARGET-MISSING" && d.ElementId == dref.Id)))
				{
					list.Add(new ValidationDiagnostic(Code: "REF-DATAOBJECTREF-TARGET-MISSING", Severity: ValidationSeverity.Error, Message: $"DataObjectReference '{dref.Id}' references missing dataObject '{dref.DataObjectRef}'", ElementId: dref.Id, Category: "Referential"));
				}
			}
		}
	}

}
