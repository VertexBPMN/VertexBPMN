using System;
using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Domain.Model.Validation;

/// <summary>
/// Precomputed graph + lookup data reused by all rules to avoid recomputation.
/// </summary>
public sealed class SemanticValidationContext
{
    public IReadOnlyDictionary<string, List<SequenceFlow>> Outgoing { get; }
    public IReadOnlyDictionary<string, List<SequenceFlow>> Incoming { get; }
    public IReadOnlyDictionary<string, FlowNode> FlowNodesById { get; }
    public IReadOnlyList<FlowNode> FlowNodes { get; }
    public IReadOnlyList<StartEvent> StartEvents { get; }
    public IReadOnlyList<EndEvent> EndEvents { get; }

    public SemanticValidationContext(BpmnModel model)
    {
        var outgoing = new Dictionary<string, List<SequenceFlow>>(StringComparer.Ordinal);
        var incoming = new Dictionary<string, List<SequenceFlow>>(StringComparer.Ordinal);
        var flowNodes = new Dictionary<string,FlowNode>(StringComparer.Ordinal);
        var allNodes = new List<FlowNode>();

        void AddNode(FlowNode? n)
        {
            if (n?.Id is null) return;
            if (!flowNodes.ContainsKey(n.Id))
            {
                flowNodes[n.Id] = n;
                allNodes.Add(n);
            }
        }

        foreach (var e in model.Events.OfType<FlowNode>()) AddNode(e);
        foreach (var g in model.Gateways.OfType<FlowNode>()) AddNode(g);
        foreach (var t in model.Tasks.OfType<FlowNode>()) AddNode(t);
        foreach (var sp in model.Subprocesses.OfType<FlowNode>()) AddNode(sp);

        foreach (var sf in model.SequenceFlows)
        {
            if (sf.SourceRef is string sid)
            {
                if (!outgoing.TryGetValue(sid, out var list))
                    outgoing[sid] = list = new List<SequenceFlow>();
                list.Add(sf);
            }
            if (sf.TargetRef is string tid)
            {
                if (!incoming.TryGetValue(tid, out var list))
                    incoming[tid] = list = new List<SequenceFlow>();
                list.Add(sf);
            }
        }

        Outgoing = outgoing;
        Incoming = incoming;
        FlowNodesById = flowNodes;
        FlowNodes = allNodes;
        StartEvents = model.Events.OfType<StartEvent>().ToList();
        EndEvents = model.Events.OfType<EndEvent>().ToList();
    }
}