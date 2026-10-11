using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Parsing;

public partial class BpmnParser
{
	private static T? OmitEmptyCollection<T>(T? collection, int? count) where T : class =>
		count == 0 ? null : collection;

	private static void CaptureDefinitionsMetadata(bool strict, Dictionary<string, string>? rawDefinitionsAttr, List<NamespacePrefix>? namespacePrefixes, XElement root)
	{
		if (strict)
		{
			foreach (var attr in root.Attributes())
			{
				if (attr.IsNamespaceDeclaration)
				{
					var prefix = (attr.Name.Namespace == XNamespace.None && attr.Name.LocalName == "xmlns") ? string.Empty : attr.Name.LocalName;
					namespacePrefixes!.Add(new NamespacePrefix(prefix, attr.Value, true));
				}
			}

			foreach (var a in root.Attributes())
			{
				if (!a.IsNamespaceDeclaration)
				{
					rawDefinitionsAttr![a.Name.ToString()] = a.Value;
				}
			}
		}
	}

	private static void CaptureProcessMetadata(bool strict, Dictionary<string, string>? rawProcessAttr, Dictionary<string, List<XElement>>? rawDocumentation, XNamespace ns, XElement process)
	{
		if (strict)
		{
			foreach (var a in process.Attributes())
			{
				rawProcessAttr![a.Name.ToString()] = a.Value;
			}

			if (rawDocumentation is not null)
			{
				var docNodes = process.Elements(ns + "documentation").Concat(process.Elements("documentation"));
				foreach (var dn in docNodes)
				{
					if (!rawDocumentation.TryGetValue("__process", out var list))
					{
						list = [];
						rawDocumentation["__process"] = list;
					}
					list.Add(new XElement(dn));
				}
			}
		}
	}

	private void WalkAdditionalProcesses(List<XElement> allProcesses, string pid, TraversalState state)
	{
		if (allProcesses.Count > 1)
		{
			if (_options.StrictValidation)
			{
				state.Diagnostics.Add($"Collaboration mit {allProcesses.Count} <process>-Elementen erkannt – alle wurden gemerged.");
			}

			foreach (var otherProcess in allProcesses.Skip(1))
			{
				state.CurrentProcessId = Intern(otherProcess.Attribute("id")?.Value ?? string.Empty);
				Walk(otherProcess, state);
			}
			state.CurrentProcessId = pid;
		}
	}

	private static void ValidateGlobalEventReferences(List<BpmnMessage> messageModels, List<BpmnSignal> signalModels, List<BpmnError> errorModels, List<BpmnEscalation> escalationModels, TraversalState state)
	{
		if (state.Events.Count > 0)
		{
			var messageIds = new HashSet<string>(messageModels.Select(m => m.Id), StringComparer.Ordinal);
			var signalIds = new HashSet<string>(signalModels.Select(s => s.Id), StringComparer.Ordinal);
			var errorIds = new HashSet<string>(errorModels.Select(e => e.Id), StringComparer.Ordinal);
			var escalationIds = new HashSet<string>(escalationModels.Select(e => e.Id), StringComparer.Ordinal);

			var eventsSpan = AsSpanSafe(state.Events);
			foreach (var ev in eventsSpan)
			{
				foreach (var def in ev.Definitions ?? [])
				{
					switch (def)
					{
						case MessageEventDefinition m when !string.IsNullOrEmpty(m.MessageRef) && !messageIds.Contains(m.MessageRef):
							state.Diagnostics.Add($"Unknown messageRef '{m.MessageRef}' at event {ev.Id}");
							break;
						case SignalEventDefinition s when !string.IsNullOrEmpty(s.SignalRef) && !signalIds.Contains(s.SignalRef):
							state.Diagnostics.Add($"Unknown signalRef '{s.SignalRef}' at event {ev.Id}");
							break;
						case ErrorEventDefinition e when !string.IsNullOrEmpty(e.ErrorRef) && !errorIds.Contains(e.ErrorRef):
							state.Diagnostics.Add($"Unknown errorRef '{e.ErrorRef}' at event {ev.Id}");
							break;
						case EscalationEventDefinition esc when !string.IsNullOrEmpty(esc.EscalationRef) && !escalationIds.Contains(esc.EscalationRef):
							state.Diagnostics.Add($"Unknown escalationRef '{esc.EscalationRef}' at event {ev.Id}");
							break;
					}
				}
			}
		}
	}

	private static void ValidateCancelEndEvents(TraversalState state)
	{
		foreach (var ev in state.Events.Where(e => e.Type == "endEvent"))
		{
			var hasCancel = state.RawEvDefs?.ContainsKey(ev.Id) == true &&
							state.RawEvDefs[ev.Id].Any(x => x.Name.LocalName == "cancelEventDefinition");
			if (!hasCancel)
			{
				hasCancel = (ev.Definitions ?? []).OfType<CancelEventDefinition>().Any();
			}

			if (hasCancel)
			{
				var cursor = ev.SubprocessId;
				var insideTx = false;
				while (cursor != null)
				{
					var sp = state.Subprocesses.FirstOrDefault(s => s.Id == cursor);
					if (sp == null)
					{
						break;
					}

					if (sp.IsTransaction)
					{
						insideTx = true;
						break;
					}

					cursor = sp.SubprocessId;
				}

				if (!insideTx)
				{
					state.Diagnostics.Add($"Cancel end event {ev.Id} outside transaction");
				}
			}
		}
	}

	private static void ValidateTerminateEndEvents(TraversalState state)
	{
		foreach (var ev in state.Events.Where(e =>
					 e.Type == "endEvent" && (e.Definitions ?? []).OfType<TerminateEventDefinition>().Any()))
		{
			var cursor = ev.SubprocessId;
			var insideTx = false;
			while (cursor != null)
			{
				var sp = state.Subprocesses.FirstOrDefault(s => s.Id == cursor);
				if (sp == null)
				{
					break;
				}

				if (sp.IsTransaction)
				{
					insideTx = true;
					break;
				}

				cursor = sp.SubprocessId;
			}

			if (!insideTx)
			{
				state.Diagnostics.Add($"Terminate end event {ev.Id} outside transaction");
			}
		}
	}

	private static void ValidateBoundaryAttachments(TraversalState state)
	{
		foreach (var (bid, attached) in state.BoundaryEvents)
		{
			if (string.IsNullOrEmpty(attached))
			{
				continue;
			}

			if (!state.FlowNodeIds.Contains(attached))
			{
				state.Diagnostics.Add($"boundaryEvent {bid} attachedToRef {attached} missing");
			}
		}
	}

	private static void ValidateCompensationActivityReferences(TraversalState state, HashSet<string> compensatableActivities)
	{
		foreach (var ev in state.Events)
		{
			foreach (var activityRef in (ev.Definitions ?? []).OfType<CompensationEventDefinition>().Select(comp => comp.ActivityRef))
			{
				if (string.IsNullOrEmpty(activityRef))
				{
					continue;
				}

				if (!state.FlowNodeIds.Contains(activityRef))
				{
					state.Diagnostics.Add($"compensateEventDefinition activityRef {activityRef} references missing activity (event {ev.Id})");
				}
				else if (!compensatableActivities.Contains(activityRef))
				{
					state.Diagnostics.Add($"compensateEventDefinition activityRef {activityRef} is not compensatable (no compensation boundary event attached; event {ev.Id})");
				}
			}
		}
	}

	private void ValidateRequiredProcessStructure(TraversalState state)
	{
		if (_options.StrictValidation)
		{
			foreach (var f in state.Flows.Where(f => !state.FlowNodeIds.Contains(f.SourceRef) || !state.FlowNodeIds.Contains(f.TargetRef)))
			{
				state.Diagnostics.Add($"SequenceFlow {f.Id} has invalid endpoints {f.SourceRef}->{f.TargetRef}");
			}

			if (!state.Events.Any(e => e.Type == "startEvent"))
			{
				state.Diagnostics.Add("No startEvent found in process");
			}
		}
	}

	private static void PopulateSubprocessChildren(TraversalState state)
	{
		if (state.Subprocesses.Count > 0)
		{
			var updated = new List<BpmnSubprocess>(state.Subprocesses.Count);
			var subprocessesSpan = CollectionsMarshal.AsSpan(state.Subprocesses);
			var eventsSpan = CollectionsMarshal.AsSpan(state.Events);
			var tasksSpan = CollectionsMarshal.AsSpan(state.Tasks);
			var gatewaysSpan = CollectionsMarshal.AsSpan(state.Gateways);
			var flowsSpan = CollectionsMarshal.AsSpan(state.Flows);

			foreach (var sp in subprocessesSpan)
			{
				var childFlowNodes = CollectSubprocessChildNodes(sp.Id, eventsSpan, tasksSpan, gatewaysSpan, subprocessesSpan);
				var childSeqFlows = new List<string>();
				foreach (var f in flowsSpan)
				{
					if (f.SubprocessId == sp.Id)
					{
						childSeqFlows.Add(f.Id);
					}
				}

				updated.Add(sp with { ChildFlowNodeIds = childFlowNodes, ChildSequenceFlowIds = childSeqFlows });
			}
			state.Subprocesses = updated;
		}
	}
	private static List<string> CollectSubprocessChildNodes(string subprocessId, ReadOnlySpan<BpmnEvent> eventsSpan, ReadOnlySpan<BpmnTask> tasksSpan, ReadOnlySpan<BpmnGateway> gatewaysSpan, ReadOnlySpan<BpmnSubprocess> subprocessesSpan)
	{
		var childFlowNodes = new List<string>();

		// Span-based filtering - much faster than LINQ Where()
		foreach (var e in eventsSpan)
		{
			if (e.SubprocessId == subprocessId)
			{
				childFlowNodes.Add(e.Id);
			}
		}

		foreach (var t in tasksSpan)
		{
			if (t.SubprocessId == subprocessId)
			{
				childFlowNodes.Add(t.Id);
			}
		}

		foreach (var g in gatewaysSpan)
		{
			if (g.SubprocessId == subprocessId)
			{
				childFlowNodes.Add(g.Id);
			}
		}

		foreach (var s2 in subprocessesSpan)
		{
			if (s2.SubprocessId == subprocessId)
			{
				childFlowNodes.Add(s2.Id);
			}
		}


		return childFlowNodes;
	}


	private void ReadCollaborationParticipants(XDocument doc, TraversalState state, List<BpmnParticipant> participantsList, List<BpmnMessageFlow> messageFlowsList)
	{
		if (_options.EnableCollaborationParsing)
		{
			var collab = doc.Descendants(state.Ns + "collaboration").FirstOrDefault();
			if (collab != null)
			{
				foreach (var part in collab.Elements(state.Ns + "participant"))
				{
					var pidAttr = part.Attribute("id")?.Value;
					var pnameAttr = part.Attribute("name")?.Value ?? string.Empty;
					var pref = part.Attribute("processRef")?.Value;
					if (!string.IsNullOrEmpty(pidAttr))
					{
						participantsList.Add(new BpmnParticipant(pidAttr, pnameAttr, pref ?? string.Empty));
					}
				}
				foreach (var mf in collab.Elements(state.Ns + "messageFlow"))
				{
					var id = mf.Attribute("id")?.Value;
					if (string.IsNullOrEmpty(id))
					{
						continue;
					}

					var name = mf.Attribute("name")?.Value ?? string.Empty;
					var src = mf.Attribute("sourceRef")?.Value ?? string.Empty;
					var tgt = mf.Attribute("targetRef")?.Value ?? string.Empty;
					messageFlowsList.Add(new BpmnMessageFlow(id, src, tgt, name));
				}
			}
		}
	}

	private void RecordParseActivity(Activity? activity, string pid, TraversalState state, List<ValidationDiagnostic>? structuredDiagnostics)
	{
		if (activity != null)
		{
			activity.SetTag("bpmn.process_id", pid);
			activity.SetTag("bpmn.node_count", (state.Events.Count + state.Tasks.Count + state.Gateways.Count + state.Subprocesses.Count).ToString());
			activity.SetTag("bpmn.flow_count", state.Flows.Count.ToString());
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
	}
}
