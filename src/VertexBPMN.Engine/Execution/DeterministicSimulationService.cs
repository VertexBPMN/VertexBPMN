using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Execution;

public sealed class DeterministicSimulationService(
    IBpmnParser parser,
    ILogger<DeterministicSimulationService> logger) : ISimulationService
{
    private const int DefaultMaxSteps = 1_000;
    private const int AbsoluteMaxSteps = 10_000;

    public async Task<SimulationResult> SimulateAsync(
        SimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BpmnXml);

        var maxSteps = request.MaxSteps ?? DefaultMaxSteps;
        if (maxSteps is < 1 or > AbsoluteMaxSteps)
            throw new ArgumentOutOfRangeException(
                nameof(request.MaxSteps),
                $"MaxSteps must be between 1 and {AbsoluteMaxSteps}.");

        var model = await parser.ParseAsync(request.BpmnXml, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.ProcessDefinitionId)
            && !string.Equals(request.ProcessDefinitionId, model.ProcessId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Requested process '{request.ProcessDefinitionId}' does not match BPMN process '{model.ProcessId}'.");
        }

        var graph = SimulationGraph.Create(model);
        graph.EnsureQualifiedModel();

        var result = new SimulationResult
        {
            BpmnXml = request.BpmnXml,
            DefinitionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.BpmnXml))),
            ProcessDefinitionId = model.ProcessId,
            TenantId = request.TenantId
        };
        var variables = request.Variables.ToDictionary(
            pair => pair.Key,
            pair => BpmnConditionEvaluator.NormalizeJsonValue(pair.Value) ?? new object(),
            StringComparer.Ordinal);
        var queue = new Queue<PendingNode>(graph.RootStartEvents.Select(node => new PendingNode(node.Id, null)));
        var blockedJoins = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var reachedRootEnd = false;

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result.Steps.Count >= maxSteps)
            {
                result.Completed = false;
                result.Message = $"Simulation stopped after reaching MaxSteps={maxSteps}.";
                return result;
            }

            var pending = queue.Dequeue();
            var node = graph.GetNode(pending.NodeId);

            if (node.IsJoin)
            {
                if (string.IsNullOrWhiteSpace(pending.IncomingFlowId))
                    throw new InvalidOperationException($"Join gateway '{node.Id}' was reached without an incoming flow.");

                if (!blockedJoins.TryGetValue(node.Id, out var arrivals))
                {
                    arrivals = new HashSet<string>(StringComparer.Ordinal);
                    blockedJoins[node.Id] = arrivals;
                }
                arrivals.Add(pending.IncomingFlowId);
                if (CanReleaseJoin(node, arrivals, queue, graph, variables))
                    ReleaseJoin(node, arrivals, blockedJoins, queue, graph, result, variables, pending.IncomingFlowId);

                ReleaseDynamicallyReadyJoins(blockedJoins, queue, graph, result, variables);
                continue;
            }

            AddStep(result, node, variables, pending.IncomingFlowId);

            switch (node.Type)
            {
                case "startEvent":
                case "task":
                case "manualTask":
                case "userTask":
                case "serviceTask":
                case "businessRuleTask":
                case "scriptTask":
                case "sendTask":
                case "receiveTask":
                case "intermediateCatchEvent":
                case "intermediateThrowEvent":
                case "boundaryEvent":
                    EnqueueFlows(queue, graph.Outgoing(node.Id));
                    break;

                case "exclusiveGateway":
                    EnqueueFlows(queue, [SelectExclusiveFlow(node, graph.Outgoing(node.Id), variables)]);
                    break;

                case "inclusiveGateway":
                case "complexGateway":
                    EnqueueFlows(queue, SelectConditionalFlows(node, graph.Outgoing(node.Id), variables));
                    break;

                case "parallelGateway":
                    EnqueueFlows(queue, graph.Outgoing(node.Id));
                    break;

                case "eventBasedGateway":
                    EnqueueFlows(queue, [SelectEventFlow(node, graph.Outgoing(node.Id), request.EventSelections)]);
                    break;

                case "subProcess":
                case "transaction":
                    var starts = graph.SubprocessStartEvents(node.Id);
                    if (starts.Count == 0)
                        EnqueueFlows(queue, graph.Outgoing(node.Id));
                    else
                        foreach (var start in starts) queue.Enqueue(new PendingNode(start.Id, null));
                    break;

                case "endEvent":
                    if (string.IsNullOrWhiteSpace(node.ParentSubprocessId))
                    {
                        reachedRootEnd = true;
                        if (node.EventDefinitionType == "terminate") queue.Clear();
                    }
                    else if (!HasPendingInScope(queue, blockedJoins, graph, node.ParentSubprocessId))
                    {
                        EnqueueFlows(queue, graph.Outgoing(node.ParentSubprocessId));
                    }
                    break;

                default:
                    throw new NotSupportedException(
                        $"Simulation does not silently approximate BPMN node type '{node.Type}' ({node.Id}).");
            }

            ReleaseDynamicallyReadyJoins(blockedJoins, queue, graph, result, variables);
        }

        if (blockedJoins.Count > 0)
        {
            result.Completed = false;
            result.Message = "Simulation ended with unresolved joins: "
                             + string.Join(", ", blockedJoins.Keys.OrderBy(key => key, StringComparer.Ordinal));
            return result;
        }

        result.Completed = reachedRootEnd;
        result.Message = reachedRootEnd
            ? "Simulation completed at a root end event."
            : "Simulation ended without reaching a root end event.";
        logger.LogInformation(
            "Deterministic simulation for {ProcessDefinitionId} completed={Completed} steps={StepCount}",
            result.ProcessDefinitionId,
            result.Completed,
            result.Steps.Count);
        return result;
    }

    private static void ReleaseDynamicallyReadyJoins(
        Dictionary<string, HashSet<string>> blockedJoins,
        Queue<PendingNode> queue,
        SimulationGraph graph,
        SimulationResult result,
        Dictionary<string, object> variables)
    {
        bool released;
        do
        {
            released = false;
            foreach (var gatewayId in blockedJoins.Keys.ToArray())
            {
                var node = graph.GetNode(gatewayId);
                var arrivals = blockedJoins[gatewayId];
                if (!CanReleaseJoin(node, arrivals, queue, graph, variables)) continue;
                ReleaseJoin(node, arrivals, blockedJoins, queue, graph, result, variables, arrivals.FirstOrDefault());
                released = true;
            }
        } while (released);
    }

    private static bool CanReleaseJoin(
        SimulationNode node,
        HashSet<string> arrivals,
        Queue<PendingNode> queue,
        SimulationGraph graph,
        Dictionary<string, object> variables)
    {
        var incoming = graph.Incoming(node.Id);
        if (node.Type == "parallelGateway")
            return incoming.All(flow => arrivals.Contains(flow.Id));

        if (node.Type == "complexGateway"
            && node.Attributes.TryGetValue("activationCondition", out var activationCondition))
        {
            var conditionVariables = new Dictionary<string, object>(variables, StringComparer.Ordinal)
            {
                ["activationCount"] = arrivals.Count
            };
            if (BpmnConditionEvaluator.Evaluate(activationCondition, conditionVariables)) return true;
        }

        return incoming
            .Where(flow => !arrivals.Contains(flow.Id))
            .All(flow => !graph.QueueCanStillReachFlow(queue, flow));
    }

    private static void ReleaseJoin(
        SimulationNode node,
        HashSet<string> arrivals,
        Dictionary<string, HashSet<string>> blockedJoins,
        Queue<PendingNode> queue,
        SimulationGraph graph,
        SimulationResult result,
        Dictionary<string, object> variables,
        string? incomingFlowId)
    {
        AddStep(result, node, variables, incomingFlowId);
        blockedJoins.Remove(node.Id);
        arrivals.Clear();
        if (node.Type == "complexGateway")
            EnqueueFlows(queue, SelectConditionalFlows(node, graph.Outgoing(node.Id), variables));
        else
            EnqueueFlows(queue, graph.Outgoing(node.Id));
    }

    private static BpmnSequenceFlow SelectExclusiveFlow(
        SimulationNode gateway,
        IReadOnlyList<BpmnSequenceFlow> outgoing,
        Dictionary<string, object> variables)
    {
        EnsureOutgoing(gateway, outgoing);
        var defaults = outgoing.Where(flow => flow.IsDefault).ToArray();
        if (defaults.Length > 1)
            throw new InvalidOperationException($"Gateway '{gateway.Id}' declares multiple default flows.");

        var selected = outgoing
            .Where(flow => !flow.IsDefault)
            .FirstOrDefault(flow => string.IsNullOrWhiteSpace(flow.ConditionExpression)
                                    || BpmnConditionEvaluator.Evaluate(flow.ConditionExpression, variables));
        return selected
               ?? defaults.SingleOrDefault()
               ?? throw new InvalidOperationException(
                   $"Exclusive gateway '{gateway.Id}' has no matching condition and no default flow.");
    }

    private static IReadOnlyList<BpmnSequenceFlow> SelectConditionalFlows(
        SimulationNode gateway,
        IReadOnlyList<BpmnSequenceFlow> outgoing,
        Dictionary<string, object> variables)
    {
        EnsureOutgoing(gateway, outgoing);
        var defaults = outgoing.Where(flow => flow.IsDefault).ToArray();
        if (defaults.Length > 1)
            throw new InvalidOperationException($"Gateway '{gateway.Id}' declares multiple default flows.");
        var selected = outgoing
            .Where(flow => !flow.IsDefault)
            .Where(flow => string.IsNullOrWhiteSpace(flow.ConditionExpression)
                           || BpmnConditionEvaluator.Evaluate(flow.ConditionExpression, variables))
            .ToArray();
        if (selected.Length > 0) return selected;
        if (defaults.Length == 1) return defaults;
        throw new InvalidOperationException(
            $"Gateway '{gateway.Id}' has no matching condition and no default flow.");
    }

    private static BpmnSequenceFlow SelectEventFlow(
        SimulationNode gateway,
        IReadOnlyList<BpmnSequenceFlow> outgoing,
        IReadOnlyDictionary<string, string> selections)
    {
        EnsureOutgoing(gateway, outgoing);
        if (outgoing.Count == 1) return outgoing[0];
        if (!selections.TryGetValue(gateway.Id, out var selection) || string.IsNullOrWhiteSpace(selection))
            throw new InvalidOperationException(
                $"Event-based gateway '{gateway.Id}' requires EventSelections['{gateway.Id}'] "
                + "with an outgoing flow id or catch-event id.");
        return outgoing.SingleOrDefault(flow =>
                   string.Equals(flow.Id, selection, StringComparison.Ordinal)
                   || string.Equals(flow.TargetRef, selection, StringComparison.Ordinal))
               ?? throw new InvalidOperationException(
                   $"Event selection '{selection}' is not an outgoing branch of gateway '{gateway.Id}'.");
    }

    private static void EnsureOutgoing(SimulationNode gateway, IReadOnlyList<BpmnSequenceFlow> outgoing)
    {
        if (outgoing.Count == 0)
            throw new InvalidOperationException($"Gateway '{gateway.Id}' has no outgoing sequence flow.");
    }

    private static void EnqueueFlows(Queue<PendingNode> queue, IEnumerable<BpmnSequenceFlow> flows)
    {
        foreach (var flow in flows)
            queue.Enqueue(new PendingNode(flow.TargetRef, flow.Id));
    }

    private static void AddStep(
        SimulationResult result,
        SimulationNode node,
        Dictionary<string, object> variables,
        string? incomingFlowId)
    {
        result.Steps.Add(new SimulationStep
        {
            StepNumber = result.Steps.Count + 1,
            ActivityId = node.Id,
            ActivityName = node.Name,
            ActivityType = node.Type,
            IncomingFlowId = incomingFlowId,
            Variables = new Dictionary<string, object>(variables, StringComparer.Ordinal),
            Timestamp = DateTime.UnixEpoch.AddMilliseconds(result.Steps.Count + 1)
        });
    }

    private static bool HasPendingInScope(
        Queue<PendingNode> queue,
        Dictionary<string, HashSet<string>> blockedJoins,
        SimulationGraph graph,
        string subprocessId) =>
        queue.Any(pending => graph.IsInScope(pending.NodeId, subprocessId))
        || blockedJoins.Keys.Any(nodeId => graph.IsInScope(nodeId, subprocessId));

    private sealed record PendingNode(string NodeId, string? IncomingFlowId);

    private sealed record SimulationNode(
        string Id,
        string Type,
        string Name,
        string? ParentSubprocessId,
        string? EventDefinitionType,
        IReadOnlyDictionary<string, string> Attributes,
        bool IsEventSubprocess = false)
    {
        public bool IsJoin { get; set; }
    }

    private sealed class SimulationGraph
    {
        private readonly Dictionary<string, SimulationNode> _nodes;
        private readonly Dictionary<string, List<BpmnSequenceFlow>> _outgoing;
        private readonly Dictionary<string, List<BpmnSequenceFlow>> _incoming;

        private SimulationGraph(
            Dictionary<string, SimulationNode> nodes,
            Dictionary<string, List<BpmnSequenceFlow>> outgoing,
            Dictionary<string, List<BpmnSequenceFlow>> incoming)
        {
            _nodes = nodes;
            _outgoing = outgoing;
            _incoming = incoming;
            RootStartEvents = nodes.Values
                .Where(node => node.Type == "startEvent" && node.ParentSubprocessId is null)
                .OrderBy(node => node.Id, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<SimulationNode> RootStartEvents { get; }

        public static SimulationGraph Create(BpmnModel model)
        {
            var nodes = new Dictionary<string, SimulationNode>(StringComparer.Ordinal);
            foreach (var @event in model.Events ?? [])
                Add(nodes, new SimulationNode(
                    @event.Id,
                    @event.Type,
                    string.IsNullOrWhiteSpace(@event.Name) ? @event.Id : @event.Name,
                    @event.SubprocessId,
                    @event.EventDefinitionType,
                    @event.Attributes ?? new Dictionary<string, string>()));
            foreach (var task in model.Tasks ?? [])
                Add(nodes, new SimulationNode(
                    task.Id,
                    task.Type,
                    string.IsNullOrWhiteSpace(task.Name) ? task.Id : task.Name,
                    task.SubprocessId,
                    null,
                    task.Attributes ?? new Dictionary<string, string>()));
            foreach (var gateway in model.Gateways ?? [])
                Add(nodes, new SimulationNode(
                    gateway.Id,
                    gateway.Type,
                    gateway.ExtensionAttributes?.GetValueOrDefault("name") ?? gateway.Id,
                    gateway.SubprocessId,
                    null,
                    gateway.ExtensionAttributes ?? new Dictionary<string, string>()));
            foreach (var subprocess in model.Subprocesses ?? [])
                Add(nodes, new SimulationNode(
                    subprocess.Id,
                    subprocess.IsTransaction ? "transaction" : "subProcess",
                    subprocess.ExtensionAttributes?.GetValueOrDefault("name") ?? subprocess.Id,
                    subprocess.SubprocessId,
                    null,
                    subprocess.ExtensionAttributes ?? new Dictionary<string, string>(),
                    subprocess.IsEventSubprocess));

            var flows = (model.SequenceFlows ?? []).ToArray();
            foreach (var flow in flows)
            {
                if (!nodes.ContainsKey(flow.SourceRef) || !nodes.ContainsKey(flow.TargetRef))
                    throw new InvalidOperationException(
                        $"Sequence flow '{flow.Id}' references an unknown source or target.");
            }
            var outgoing = flows.GroupBy(flow => flow.SourceRef, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            var incoming = flows.GroupBy(flow => flow.TargetRef, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            foreach (var node in nodes.Values.Where(node => node.Type is "parallelGateway" or "inclusiveGateway" or "complexGateway"))
                node.IsJoin = incoming.GetValueOrDefault(node.Id)?.Count > 1;
            return new SimulationGraph(nodes, outgoing, incoming);
        }

        public void EnsureQualifiedModel()
        {
            if (RootStartEvents.Count == 0)
                throw new InvalidOperationException("The BPMN process has no root start event.");
            var unsupported = _nodes.Values.FirstOrDefault(node =>
                node.Type == "callActivity" || node.IsEventSubprocess);
            if (unsupported is not null)
                throw new NotSupportedException(
                    $"Simulation does not silently approximate '{unsupported.Type}' node '{unsupported.Id}'.");
        }

        public SimulationNode GetNode(string id) =>
            _nodes.TryGetValue(id, out var node)
                ? node
                : throw new InvalidOperationException($"Flow node '{id}' is not defined.");

        public IReadOnlyList<BpmnSequenceFlow> Outgoing(string nodeId) =>
            _outgoing.GetValueOrDefault(nodeId) ?? [];

        public IReadOnlyList<BpmnSequenceFlow> Incoming(string nodeId) =>
            _incoming.GetValueOrDefault(nodeId) ?? [];

        public IReadOnlyList<SimulationNode> SubprocessStartEvents(string subprocessId) =>
            _nodes.Values
                .Where(node => node.Type == "startEvent"
                               && string.Equals(node.ParentSubprocessId, subprocessId, StringComparison.Ordinal))
                .OrderBy(node => node.Id, StringComparer.Ordinal)
                .ToArray();

        public bool IsInScope(string nodeId, string subprocessId)
        {
            var current = GetNode(nodeId).ParentSubprocessId;
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (string.Equals(current, subprocessId, StringComparison.Ordinal)) return true;
                current = GetNode(current).ParentSubprocessId;
            }
            return false;
        }

        public bool QueueCanStillReachFlow(Queue<PendingNode> queue, BpmnSequenceFlow targetFlow) =>
            queue.Any(pending =>
                string.Equals(pending.IncomingFlowId, targetFlow.Id, StringComparison.Ordinal)
                || CanReach(pending.NodeId, targetFlow.SourceRef));

        private bool CanReach(string startNodeId, string targetNodeId)
        {
            var pending = new Stack<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            pending.Push(startNodeId);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!visited.Add(current)) continue;
                if (string.Equals(current, targetNodeId, StringComparison.Ordinal)) return true;
                foreach (var flow in Outgoing(current)) pending.Push(flow.TargetRef);
            }
            return false;
        }

        private static void Add(Dictionary<string, SimulationNode> nodes, SimulationNode node)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || !nodes.TryAdd(node.Id, node))
                throw new InvalidOperationException($"Duplicate or empty BPMN flow-node id '{node.Id}'.");
        }
    }
}
