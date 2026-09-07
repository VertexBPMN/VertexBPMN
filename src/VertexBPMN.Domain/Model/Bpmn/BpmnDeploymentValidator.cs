namespace VertexBPMN.Domain.Model.Bpmn;

/// <summary>Execution-path checks, separate from advisory round-trip parser diagnostics.</summary>
public static class BpmnDeploymentValidator
{
    private sealed record Node(string Id, string Scope, string Type, bool Independent = false);

    public static IReadOnlyList<ValidationDiagnostic> Validate(BpmnModel model)
    {
        var issues = new List<ValidationDiagnostic>();
        string Scope(string? process, string? parent) => parent ?? process ?? model.ProcessId;
        bool Compensation(Dictionary<string, string>? attributes) =>
            attributes?.GetValueOrDefault("isForCompensation") is "true" or "1";
        var events = model.Events ?? [];
        var gateways = model.Gateways ?? [];
        var nodes = events.Select(e => new Node(e.Id, Scope(e.ProcessId, e.SubprocessId), e.Type))
            .Concat((model.Tasks ?? []).Select(t => new Node(t.Id, Scope(t.ProcessId, t.SubprocessId), t.Type, Compensation(t.Attributes))))
            .Concat(gateways.Select(g => new Node(g.Id, Scope(g.ProcessId, g.SubprocessId), g.Type)))
            .Concat((model.Subprocesses ?? []).Select(s => new Node(s.Id, Scope(s.ProcessId, s.SubprocessId), "subProcess", s.IsEventSubprocess || Compensation(s.Attributes))))
            .ToArray();
        var byId = nodes.GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
        var adjacency = nodes.Select(n => n.Id).Distinct().ToDictionary(id => id, _ => new List<string>());
        void Error(string code, string id, string message) => issues.Add(new ValidationDiagnostic(code, ValidationSeverity.Error, message, id, "Deployment"));
        foreach (var flow in model.SequenceFlows ?? [])
        {
            if (!byId.TryGetValue(flow.SourceRef, out var source) || !byId.TryGetValue(flow.TargetRef, out var target) || source.Scope != target.Scope)
                Error("DEP-FLOW-ENDPOINT", flow.Id, $"Sequence flow '{flow.Id}' must connect flow nodes in the same scope.");
            else
                adjacency[source.Id].Add(target.Id);
        }
        // Boundary events are activated by their attached activity, not a sequence flow.
        foreach (var boundary in events.Where(e => e.Type == "boundaryEvent"))
            if (adjacency.TryGetValue(boundary.AttachedToRef, out var targets)) targets.Add(boundary.Id);
        // Link events transfer control without an XML sequence flow between throw and catch.
        foreach (var throwing in events.Where(e => e.Type == "intermediateThrowEvent"))
            foreach (var link in (throwing.Definitions ?? []).OfType<LinkEventDefinition>())
                foreach (var catching in events.Where(e => e.Type == "intermediateCatchEvent"
                    && Scope(e.ProcessId, e.SubprocessId) == Scope(throwing.ProcessId, throwing.SubprocessId)
                    && (e.Definitions ?? []).OfType<LinkEventDefinition>().Any(c => c.Name == link.Name)))
                    adjacency[throwing.Id].Add(catching.Id);
        foreach (var scope in nodes.GroupBy(n => n.Scope))
        {
            var starts = scope.Where(n => n.Type == "startEvent").ToArray();
            // BPMN permits implicit starts in scopes without an explicit StartEvent.
            if (starts.Length == 0) continue;
            var reached = new HashSet<string>();
            var queue = new Queue<string>(starts.Select(n => n.Id).Concat(scope.Where(n => n.Independent).Select(n => n.Id)));
            while (queue.TryDequeue(out var id))
                if (reached.Add(id)) foreach (var target in adjacency[id]) queue.Enqueue(target);
            foreach (var node in scope.Where(n => !reached.Contains(n.Id)))
                Error("DEP-UNREACHABLE-NODE", node.Id, $"Flow node '{node.Id}' is unreachable from the start events of its scope.");
        }
        foreach (var gateway in gateways.Where(g => g.Type is "exclusiveGateway" or "inclusiveGateway"))
        {
            var outgoing = (model.SequenceFlows ?? []).Where(f => f.SourceRef == gateway.Id).ToArray();
            if (outgoing.Length == 0)
                Error("DEP-GATEWAY-OUTGOING", gateway.Id, $"Gateway '{gateway.Id}' has no outgoing sequence flow.");
            if (outgoing.Length > 1)
                foreach (var flow in outgoing.Where(f => f.Id != gateway.DefaultFlowId && !f.IsDefault && string.IsNullOrWhiteSpace(f.ConditionExpression)))
                    Error("DEP-GATEWAY-CONDITION", flow.Id, $"Configure a condition on non-default branch '{flow.Id}'.");
        }
        return issues;
    }
}
