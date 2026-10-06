using System.Collections.ObjectModel;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Domain.Model.Runtime;

public static class RuntimeProjectionBuilder
{
	public static RuntimeProcessModel Build(
		BpmnParserOptions options,
		string processId,
		IReadOnlyList<BpmnEvent> events,
		IReadOnlyList<BpmnTask> tasks,
		IReadOnlyList<BpmnGateway> gateways,
		IReadOnlyList<BpmnSubprocess> subprocesses,
		IReadOnlyList<BpmnSequenceFlow> flows,
		IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? vendorNormalized,
		BpmnRawMetadata? rawMetadata,
		IReadOnlyDictionary<string, (string? Format, string? Body, string? Result)>? scriptTaskRaw,
		IReadOnlyDictionary<string, string>? potentialOwnerExtras // NEW
	)
	{
		var flowNodes = BuildFlowNodes(events, tasks, gateways, subprocesses, flows, rawMetadata);
		var rtFlows = flows.Select(f => new RuntimeSequenceFlow(f.Id, f.SourceRef, f.TargetRef, f.IsDefault))
			.Where(f => !string.IsNullOrEmpty(f.Id)).ToList();
		var vx = FilterVendorExtensions(options, vendorNormalized, flowNodes);
		var scriptTasks = BuildScriptTasks(scriptTaskRaw);
		ReadOnlyDictionary<string, string>? potentialOwners = null;
		if (potentialOwnerExtras is { Count: > 0 })
		{
			potentialOwners = new ReadOnlyDictionary<string, string>(
				potentialOwnerExtras.ToDictionary(k => k.Key, v => v.Value, StringComparer.Ordinal));
		}
		return new RuntimeProcessModel(processId, flowNodes, rtFlows, vx, scriptTasks, potentialOwners);
	}

	private static List<RuntimeFlowNode> BuildFlowNodes(IReadOnlyList<BpmnEvent> events,
		IReadOnlyList<BpmnTask> tasks, IReadOnlyList<BpmnGateway> gateways,
		IReadOnlyList<BpmnSubprocess> subprocesses, IReadOnlyList<BpmnSequenceFlow> flows, BpmnRawMetadata? rawMetadata)
	{
		var defaultTargetIds = new HashSet<string>(flows
			.Where(f => f.IsDefault)
			.Select(f => f.TargetRef)
			.Where(id => !string.IsNullOrEmpty(id)),
			StringComparer.Ordinal);

		var (miIds, miSeqIds) = FindMultiInstanceNodes(subprocesses, tasks, rawMetadata);
		var flowNodes = new List<RuntimeFlowNode>(events.Count + tasks.Count + gateways.Count + subprocesses.Count);
		void AddNode(string id, string type, string? parent, bool mi, bool miSeq, bool isEventSp)
		{
			if (!string.IsNullOrEmpty(id))
			{
				flowNodes.Add(new RuntimeFlowNode(id, type, parent, mi, miSeq, isEventSp, defaultTargetIds.Contains(id)));
			}
		}
		foreach (var e in events)
		{
			AddNode(e.Id, e.Type, e.SubprocessId, miIds.Contains(e.Id), miSeqIds.Contains(e.Id), false);
		}
		foreach (var t in tasks)
		{
			AddNode(t.Id, t.Type, t.SubprocessId, miIds.Contains(t.Id), miSeqIds.Contains(t.Id), false);
		}
		foreach (var g in gateways)
		{
			AddNode(g.Id, g.Type, g.SubprocessId, false, false, false);
		}
		foreach (var sp in subprocesses)
		{
			AddNode(sp.Id, "subProcess", sp.SubprocessId, miIds.Contains(sp.Id), miSeqIds.Contains(sp.Id), sp.IsEventSubprocess);
		}
		return flowNodes;
	}

	private static (HashSet<string> Ids, HashSet<string> SequentialIds) FindMultiInstanceNodes(
		IReadOnlyList<BpmnSubprocess> subprocesses, IReadOnlyList<BpmnTask> tasks, BpmnRawMetadata? rawMetadata)
	{
		var miIds = new HashSet<string>(StringComparer.Ordinal);
		var miSeqIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (var sp in subprocesses)
		{
			if (sp.Loop is MultiInstanceLoopCharacteristics mi)
			{
				miIds.Add(sp.Id);
				if (mi.IsSequential)
				{
					miSeqIds.Add(sp.Id);
				}
			}
		}

		var rawMi = rawMetadata?.RawMultiInstance;
		if (rawMi is { Count: > 0 })
		{
			foreach (var kv in rawMi.Where(kv => kv.Value.Name.LocalName is "multiInstanceLoopCharacteristics"))
			{
					var id = kv.Key;
					if (string.IsNullOrEmpty(id))
					{
						continue;
					}

					if (tasks.Any(t => t.Id == id))
					{
						miIds.Add(id);
						if (kv.Value.Attribute("isSequential")?.Value == "true")
						{
							miSeqIds.Add(id);
						}
					}
			}
		}
		return (miIds, miSeqIds);
	}

	private static Dictionary<string, IReadOnlyDictionary<string, string>>? FilterVendorExtensions(
		BpmnParserOptions options, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? vendorNormalized,
		List<RuntimeFlowNode> flowNodes)
	{
		Dictionary<string, IReadOnlyDictionary<string, string>>? vx = null;
		if (options.NormalizeVendorExtensions && vendorNormalized is { Count: > 0 })
		{
			var nodeIdSet = new HashSet<string>(flowNodes.Select(n => n.Id), StringComparer.Ordinal);
			var filtered = vendorNormalized
				.Where(kv => nodeIdSet.Contains(kv.Key))
				.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
			if (filtered.Count > 0)
			{
				vx = filtered;
			}
		}
		else if (!options.NormalizeVendorExtensions && vendorNormalized is { Count: > 0 })
		{
			// We only keep potentialOwner extras in this mode (filter keys to those that have potentialOwner).
			var poFiltered = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
			foreach (var kv in vendorNormalized.Where(kv => kv.Value.Keys.Any(k => k.Contains("potentialOwner", StringComparison.Ordinal))))
			{
					poFiltered[kv.Key] = kv.Value;
			}
			if (poFiltered.Count > 0)
			{
				vx = poFiltered;
			}
		}

		return vx;
	}

	private static Dictionary<string, RuntimeScriptTask>? BuildScriptTasks(
		IReadOnlyDictionary<string, (string? Format, string? Body, string? Result)>? scriptTaskRaw)
	{
		Dictionary<string, RuntimeScriptTask>? scriptTasks = null;
		if (scriptTaskRaw is { Count: > 0 })
		{
			var dict = new Dictionary<string, RuntimeScriptTask>(scriptTaskRaw.Count, StringComparer.Ordinal);
			foreach (var kv in scriptTaskRaw)
			{
				if (!string.IsNullOrEmpty(kv.Key) &&
					(!string.IsNullOrWhiteSpace(kv.Value.Body) || !string.IsNullOrWhiteSpace(kv.Value.Format)))
				{
					dict[kv.Key] = new RuntimeScriptTask(
						kv.Value.Format ?? string.Empty,
						kv.Value.Body ?? string.Empty,
						kv.Value.Result
					);
				}
			}
			if (dict.Count > 0)
			{
				scriptTasks = dict;
			}
		}

		return scriptTasks;
	}
}
