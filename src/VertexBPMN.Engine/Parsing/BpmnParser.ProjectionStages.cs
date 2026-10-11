using System.Collections.ObjectModel;
using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Parsing;

public partial class BpmnParser
{
	private static void ValidateTraversalActivityRules(TraversalState state)
	{
		// Additional validations
		foreach (var cid in state.PendingMiConflicts.Where(cid => !string.IsNullOrEmpty(cid)))
		{
			state.Diagnostics.Add($"multi-instance conflict on {cid}");
		}

		foreach (var f in state.Flows.Where(f => f.IsDefault))
		{
			var hasCond = !string.IsNullOrWhiteSpace(f.ConditionExpression) || (state.RawCond != null &&
				state.RawCond.TryGetValue(f.Id, out var rc) && !string.IsNullOrEmpty(rc.Raw));
			if (hasCond)
			{
				state.Diagnostics.Add($"Default flow {f.Id} has condition");
			}
		}

		ValidateCancelEndEvents(state);

		ValidateTerminateEndEvents(state);

		foreach (var gw in state.Gateways.Where(gw => !state.Flows.Any(f => f.SourceRef == gw.Id)))
		{
			state.Diagnostics.Add($"Gateway {gw.Id} has no outgoing");
		}

		ValidateBoundaryAttachments(state);

		foreach (var bevId in state.Events.Where(e => e.Type == "boundaryEvent" && (e.Definitions ?? []).OfType<CompensationEventDefinition>().Any()).Select(bev => bev.Id))
		{
			if (state.ElementsMetadata != null && state.ElementsMetadata.TryGetValue(bevId, out var meta) && (!meta.Attributes.TryGetValue("cancelActivity", out var val) || !string.Equals(val, "false", StringComparison.OrdinalIgnoreCase)))
			{
				state.Diagnostics.Add($"Boundary compensation event {bevId} must have cancelActivity='false'");
			}
		}
		var compensatableActivities = state.Events
			.Where(e => e.Type == "boundaryEvent"
						&& (e.Definitions ?? []).OfType<CompensationEventDefinition>().Any())
			.Select(e => e.AttachedToRef)
			.Where(a => !string.IsNullOrEmpty(a))
			.ToHashSet(StringComparer.Ordinal);

		ValidateCompensationActivityReferences(state, compensatableActivities);

	}

	private void ValidateTraversalLinkRules(TraversalState state)
	{
		foreach (var bevId in state.Events.Where(e => e.Type == "boundaryEvent" && (e.Definitions ?? []).OfType<EscalationEventDefinition>().Any()).Select(bev => bev.Id))
		{
			if (state.ElementsMetadata != null && state.ElementsMetadata.TryGetValue(bevId, out var meta) && (!meta.Attributes.TryGetValue("escalationRef", out var es) || !string.IsNullOrEmpty(es)))
			{
				state.Diagnostics.Add($"Unknown escalationRef fir boundaryEvent {bevId}");
			}
		}

		foreach (var kv in state.LinkThrowCounts)
		{
			if (kv.Value > 1)
			{
				state.Diagnostics.Add($"Multiple throw link events for {kv.Key}");
			}
		}

		foreach (var kv in state.LinkThrowCounts.Keys.Where(kv => !state.LinkCatchNames.Contains(kv)))
		{
			state.Diagnostics.Add($"Unmatched link {kv}");
		}

		foreach (var f in state.Flows.Where(f => f.IsDefault))
		{
			var hasCond = !string.IsNullOrWhiteSpace(f.ConditionExpression) || (state.RawCond != null &&
				state.RawCond.TryGetValue(f.Id, out var rc) && !string.IsNullOrEmpty(rc.Raw));
			if (hasCond)
			{
				state.Diagnostics.Add($"Default flow {f.Id} has condition");
			}
		}

		ValidateRequiredProcessStructure(state);

	}

	private (List<BpmnShape>? Shapes, List<BpmnEdge>? Edges, XElement? RawRoot) ReadDiagramInterchange(XDocument doc, TraversalState state)
	{
		List<BpmnShape>? shapes = null;
		List<BpmnEdge>? edges = null;
		XElement? diRoot = null;
		if (_options.ParseDiagramInterchange)
		{
			var bpmndi = (XNamespace)"http://www.omg.org/spec/BPMN/20100524/DI";
			var omgdc = (XNamespace)"http://www.omg.org/spec/DD/20100524/DC";
			var omgdi = (XNamespace)"http://www.omg.org/spec/DD/20100524/DI";
			shapes = [];
			edges = [];
			foreach (var shape in doc.Descendants(bpmndi + "BPMNShape"))
			{
				var id = Intern(shape.Attribute("id")?.Value ?? string.Empty);
				var bpmnElement = Intern(shape.Attribute("bpmnElement")?.Value ?? string.Empty);
				var bounds = shape.Element(omgdc + "Bounds");
				if (bounds != null && double.TryParse(bounds.Attribute("x")?.Value, out var x) &&
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
				{
					if (double.TryParse(waypoint.Attribute("x")?.Value, out var wx) &&
						double.TryParse(waypoint.Attribute("y")?.Value, out var wy))
					{
						wp.Add((wx, wy));
					}
				}

				edges.Add(new BpmnEdge(id, bpmnElement, wp));
			}

			if (state.Strict && shapes.Count + edges.Count > 0 && _options.CaptureDiRaw)
			{
				diRoot = doc.Descendants(bpmndi + "BPMNDiagram").FirstOrDefault()?.Parent;
			}
		}

		return (shapes, edges, diRoot);
	}

	private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? MergePotentialOwnerExtensions(
		TraversalState state, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? vendorNormalized)
	{
		if (state.PotentialOwnerExtras.Count > 0)
		{
			// Ensure dictionary exists
			var merged = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
			if (vendorNormalized != null)
			{
				foreach (var kv in vendorNormalized)
				{
					merged[kv.Key] = kv.Value;
				}
			}
			foreach (var kv in state.PotentialOwnerExtras)
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

		return vendorNormalized;
	}
}
