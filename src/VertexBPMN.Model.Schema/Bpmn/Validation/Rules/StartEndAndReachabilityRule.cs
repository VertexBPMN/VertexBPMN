using System.Collections.Generic;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Domain.Model.Bpmn.Validation;

namespace VertexBPMN.Domain.Model.Bpmn.Validation.Rules;

internal sealed class StartEndAndReachabilityRule : IBpmnSemanticRule
{
    public IEnumerable<ValidationDiagnostic> Evaluate(BpmnModel model, SemanticValidationContext ctx)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        // BPMN001 / 002
        if (ctx.StartEvents.Count == 0)
            diagnostics.Add(Error("BPMN001", "No startEvent found -> Fix: add a startEvent", model.ProcessId, "ProcessStructure"));
        if (ctx.EndEvents.Count == 0)
            diagnostics.Add(Error("BPMN002", "No endEvent found -> Fix: add an endEvent", model.ProcessId, "ProcessStructure"));

        // BPMN003 startEvents must not have incoming
        foreach (var se in ctx.StartEvents)
        {
            if (se.Id != null && ctx.Incoming.ContainsKey(se.Id))
                diagnostics.Add(Error("BPMN003", $"StartEvent '{se.Id}' has incoming SequenceFlows", se.Id, "ProcessStructure"));
        }

        // BPMN004 endEvents must not have outgoing
        foreach (var ee in ctx.EndEvents)
        {
            if (ee.Id != null && ctx.Outgoing.ContainsKey(ee.Id))
                diagnostics.Add(Error("BPMN004", $"EndEvent '{ee.Id}' has outgoing SequenceFlows", ee.Id, "ProcessStructure"));
        }

        // Reachability BFS (BPMN010 / BPMN011)
        var reachable = new HashSet<string>();
        var stack = new Stack<string>(ctx.StartEvents.Where(e => e.Id != null).Select(e => e.Id!));
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!reachable.Add(current)) continue;
            if (ctx.Outgoing.TryGetValue(current, out var outs))
            {
                foreach (var sf in outs)
                    if (sf.TargetRef is { } tid) stack.Push(tid);
            }
        }

        foreach (var fn in ctx.FlowNodes)
        {
            if (fn.Id == null) continue;
            if (ctx.StartEvents.Any(s => s.Id == fn.Id)) continue;
            if (!reachable.Contains(fn.Id))
                diagnostics.Add(Error("BPMN010", $"FlowNode '{fn.Id}' unreachable from any StartEvent", fn.Id, "Graph"));
            else if (!ctx.Outgoing.ContainsKey(fn.Id) && fn is not EndEvent)
                diagnostics.Add(Warning("BPMN011", $"FlowNode '{fn.Id}' has no outgoing SequenceFlow (potential dead-end)", fn.Id, "Graph"));
        }

        return diagnostics;
    }

    private static ValidationDiagnostic Error(string c, string m, string? id, string cat)
        => new(c, ValidationSeverity.Error, m, id, cat);
    private static ValidationDiagnostic Warning(string c, string m, string? id, string cat)
        => new(c, ValidationSeverity.Warning, m, id, cat);
}