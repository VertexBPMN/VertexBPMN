using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Parsing;

internal static class RuntimeProjectionBuilder
{
    internal static RuntimeProcessModel Build(
        BpmnParserOptions options,
        string processId,
        IReadOnlyList<BpmnEvent> events,
        IReadOnlyList<BpmnTask> tasks,
        IReadOnlyList<BpmnGateway> gateways,
        IReadOnlyList<BpmnSubprocess> subprocesses,
        IReadOnlyList<BpmnSequenceFlow> flows,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string,string>>? vendorNormalized)
    {
        // Pre-size list
        var flowNodes = new List<RuntimeFlowNode>(
            events.Count + tasks.Count + gateways.Count + subprocesses.Count);

        void AddNode(string id, string type, string? parent, bool mi, bool miSeq, bool isEventSp)
        {
            if (string.IsNullOrEmpty(id)) return; // skip invalid
            flowNodes.Add(new RuntimeFlowNode(id, type, parent, mi, miSeq, isEventSp, false));
        }

        // Build quick MI lookup only from subprocess loop characteristics (task loop characteristics not modeled yet)
        var miIds = new HashSet<string>(StringComparer.Ordinal);
        var miSeqIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sp in subprocesses)
        {
            if (sp.Loop is MultiInstanceLoopCharacteristics mi)
            {
                if (!string.IsNullOrEmpty(sp.Id))
                {
                    miIds.Add(sp.Id);
                    if (mi.IsSequential) miSeqIds.Add(sp.Id);
                }
            }
        }

        // Events
        foreach (var e in events)
            AddNode(e.Id, e.Type, e.SubprocessId,
                miIds.Contains(e.Id), miSeqIds.Contains(e.Id),
                false);
        // Tasks (no loop detection yet)
        foreach (var t in tasks)
            AddNode(t.Id, t.Type, t.SubprocessId, false, false, false);
        // Gateways
        foreach (var g in gateways)
            AddNode(g.Id, g.Type, g.SubprocessId, false, false, false);
        // Subprocesses (loop + event subprocess flag)
        foreach (var sp in subprocesses)
            AddNode(sp.Id, "subProcess", sp.SubprocessId,
                miIds.Contains(sp.Id), miSeqIds.Contains(sp.Id), sp.IsEventSubprocess);

        // Sequence flows
        var rtFlows = flows.Select(f =>
            new RuntimeSequenceFlow(f.Id, f.SourceRef, f.TargetRef, f.IsDefault))
            .Where(f => !string.IsNullOrEmpty(f.Id))
            .ToList();

        // Filter vendor extensions: only keep those for node ids we have
        IReadOnlyDictionary<string, IReadOnlyDictionary<string,string>>? vx = null;
        if (options.NormalizeVendorExtensions && vendorNormalized is { Count: > 0 })
        {
            var nodeIdSet = new HashSet<string>(flowNodes.Select(n => n.Id), StringComparer.Ordinal);
            var filtered = vendorNormalized
                .Where(kv => nodeIdSet.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
            if (filtered.Count > 0)
                vx = filtered;
        }

        return new RuntimeProcessModel(
            processId,
            flowNodes,
            rtFlows,
            vx);
    }
}