using System.Collections;
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
		{
			throw new ArgumentOutOfRangeException(nameof(request), request.MaxSteps,
				$"MaxSteps must be between 1 and {AbsoluteMaxSteps}.");
		}
		var model = await parser.ParseAsync(request.BpmnXml, cancellationToken);
		if (!string.IsNullOrWhiteSpace(request.ProcessDefinitionId)
			&& !string.Equals(request.ProcessDefinitionId, model.ProcessId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Requested process '{request.ProcessDefinitionId}' does not match BPMN process '{model.ProcessId}'.");
		}
		var state = CreateSimulationState(request, model, maxSteps);
		if (!await RunSimulationAsync(state, cancellationToken))
		{
			return state.Result;
		}
		CompleteSimulation(state);
		return state.Result;
	}

	private sealed class SimulationState
	{
		public required SimulationRequest Request { get; init; }
		public required SimulationGraph Graph { get; init; }
		public required SimulationResult Result { get; init; }
		public required Dictionary<string, object> Variables { get; init; }
		public required Queue<PendingNode> Queue { get; init; }
		public Dictionary<JoinKey, HashSet<string>> BlockedJoins { get; } = [];
		public required int MaxSteps { get; init; }
		public bool ReachedRootEnd { get; set; }
	}

	private static SimulationState CreateSimulationState(SimulationRequest request, BpmnModel model, int maxSteps)
	{
		var graph = SimulationGraph.Create(model);
		graph.EnsureQualifiedModel();
		var selectedStarts = graph.SelectedEventSubprocessStarts(request.EventSelections);
		var interruptingStarts = selectedStarts.Where(SimulationGraph.IsInterruptingEventSubprocessStart).ToArray();
		var initialNodes = interruptingStarts.Length > 0
			? interruptingStarts : [.. graph.RootStartEvents, .. selectedStarts];
		return new SimulationState
		{
			Request = request,
			Graph = graph,
			MaxSteps = maxSteps,
			Queue = new Queue<PendingNode>(initialNodes.Select(node => new PendingNode(node.Id, null))),
			Variables = request.Variables.ToDictionary(pair => pair.Key,
				pair => BpmnConditionEvaluator.NormalizeJsonValue(pair.Value) ?? new object(), StringComparer.Ordinal),
			Result = new SimulationResult
			{
				BpmnXml = request.BpmnXml,
				DefinitionHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.BpmnXml))),
				ProcessDefinitionId = model.ProcessId,
				TenantId = request.TenantId
			}
		};
	}

	private async Task<bool> RunSimulationAsync(SimulationState state, CancellationToken cancellationToken)
	{
		while (state.Queue.Count > 0)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (state.Result.Steps.Count >= state.MaxSteps)
			{
				StopAtStepLimit(state);
				return false;
			}
			var pending = state.Queue.Dequeue();
			var node = state.Graph.GetNode(pending.NodeId);
			var effectiveVariables = EffectiveVariables(state.Variables, pending.MultiInstanceContexts);
			if (node.IsJoin)
			{
				ProcessJoinArrival(state, node, pending, effectiveVariables);
			}
			else
			{
				AddStep(state.Result, node, effectiveVariables, pending.IncomingFlowId);
				if (!await ProcessSimulationNodeAsync(state, node, pending, effectiveVariables, cancellationToken))
				{
					return false;
				}
			}
			ReleaseDynamicallyReadyJoins(state.BlockedJoins, state.Queue, state.Graph, state.Result, state.Variables);
		}
		return true;
	}

	private static void ProcessJoinArrival(SimulationState state, SimulationNode node, PendingNode pending,
		Dictionary<string, object> effectiveVariables)
	{
		if (string.IsNullOrWhiteSpace(pending.IncomingFlowId))
		{
			throw new InvalidOperationException($"Join gateway '{node.Id}' was reached without an incoming flow.");
		}
		var key = new JoinKey(node.Id, pending.ExecutionKey, pending.MultiInstanceContexts);
		if (!state.BlockedJoins.TryGetValue(key, out var arrivals))
		{
			arrivals = new HashSet<string>(StringComparer.Ordinal);
			state.BlockedJoins[key] = arrivals;
		}
		arrivals.Add(pending.IncomingFlowId);
		if (CanReleaseJoin(node, arrivals, state.Queue, state.Graph, effectiveVariables, pending.ExecutionKey))
		{
			ReleaseJoin(key, node, arrivals, state.BlockedJoins, state.Queue, state.Graph, state.Result,
				effectiveVariables, pending.IncomingFlowId);
		}
	}

	private async Task<bool> ProcessSimulationNodeAsync(SimulationState state, SimulationNode node,
		PendingNode pending, Dictionary<string, object> effectiveVariables, CancellationToken cancellationToken)
	{
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
			case "parallelGateway":
				EnqueueFlows(state.Queue, state.Graph.Outgoing(node.Id), pending.MultiInstanceContexts);
				break;
			case "exclusiveGateway":
				EnqueueFlows(state.Queue, [SelectExclusiveFlow(node, state.Graph.Outgoing(node.Id), effectiveVariables)],
					pending.MultiInstanceContexts);
				break;
			case "inclusiveGateway":
			case "complexGateway":
				EnqueueFlows(state.Queue, SelectConditionalFlows(node, state.Graph.Outgoing(node.Id), effectiveVariables),
					pending.MultiInstanceContexts);
				break;
			case "eventBasedGateway":
				EnqueueFlows(state.Queue, [SelectEventFlow(node, state.Graph.Outgoing(node.Id), state.Request.EventSelections)],
					pending.MultiInstanceContexts);
				break;
			case "callActivity":
				return await SimulateCallActivityAsync(state, node, pending, effectiveVariables, cancellationToken);
			case "subProcess":
			case "transaction":
				StartSimulatedSubprocess(state, node, pending, effectiveVariables);
				break;
			case "endEvent":
				CompleteSimulatedScope(state, node, pending);
				break;
			default:
				throw new NotSupportedException(
					$"Simulation does not silently approximate BPMN node type '{node.Type}' ({node.Id}).");
		}
		return true;
	}

	private async Task<bool> SimulateCallActivityAsync(SimulationState state, SimulationNode node, PendingNode pending,
		Dictionary<string, object> effectiveVariables, CancellationToken cancellationToken)
	{
		if (!node.Attributes.TryGetValue("calledElement", out var calledElement)
			|| string.IsNullOrWhiteSpace(calledElement))
		{
			throw new InvalidOperationException($"Call activity '{node.Id}' has no calledElement.");
		}
		if (!state.Request.CalledProcessDefinitions.TryGetValue(calledElement, out var calledBpmn)
			|| string.IsNullOrWhiteSpace(calledBpmn))
		{
			throw new InvalidOperationException(
				$"Simulation requires CalledProcessDefinitions['{calledElement}'] for call activity '{node.Id}'.");
		}
		var remainingSteps = state.MaxSteps - state.Result.Steps.Count;
		if (remainingSteps <= 0)
		{
			StopAtStepLimit(state);
			return false;
		}
		var calledResult = await SimulateAsync(new SimulationRequest
		{
			BpmnXml = calledBpmn,
			ProcessDefinitionId = calledElement,
			Variables = new Dictionary<string, object>(effectiveVariables, StringComparer.Ordinal),
			MaxSteps = remainingSteps,
			TenantId = state.Request.TenantId,
			EventSelections = state.Request.EventSelections,
			CalledProcessDefinitions = state.Request.CalledProcessDefinitions
		}, cancellationToken);
		AppendCalledSteps(state.Result, calledResult);
		if (!calledResult.Completed)
		{
			state.Result.Completed = false;
			state.Result.Message = $"Called process '{calledElement}' did not complete: {calledResult.Message}";
			return false;
		}
		if (calledResult.Steps.LastOrDefault() is { } lastCalledStep)
		{
			foreach (var variable in lastCalledStep.Variables)
			{
				state.Variables[variable.Key] = variable.Value;
			}
		}
		EnqueueFlows(state.Queue, state.Graph.Outgoing(node.Id), pending.MultiInstanceContexts);
		return true;
	}

	private static void AppendCalledSteps(SimulationResult result, SimulationResult calledResult)
	{
		foreach (var step in calledResult.Steps)
		{
			result.Steps.Add(new SimulationStep
			{
				StepNumber = result.Steps.Count + 1,
				ActivityId = step.ActivityId,
				ActivityName = step.ActivityName,
				ActivityType = step.ActivityType,
				IncomingFlowId = step.IncomingFlowId,
				Variables = new Dictionary<string, object>(step.Variables, StringComparer.Ordinal),
				Timestamp = DateTime.UnixEpoch.AddMilliseconds(result.Steps.Count + 1)
			});
		}
	}

	private static void StartSimulatedSubprocess(SimulationState state, SimulationNode node, PendingNode pending,
		Dictionary<string, object> effectiveVariables)
	{
		var starts = state.Graph.SubprocessStartEvents(node.Id);
		if (starts.Length == 0)
		{
			EnqueueFlows(state.Queue, state.Graph.Outgoing(node.Id), pending.MultiInstanceContexts);
		}
		else if (node.Loop is MultiInstanceLoopCharacteristics loop)
		{
			var items = ResolveMultiInstanceItems(loop, effectiveVariables);
			if (items.Length == 0)
			{
				EnqueueFlows(state.Queue, state.Graph.Outgoing(node.Id), pending.MultiInstanceContexts);
				return;
			}
			var indexes = loop.IsSequential ? [0] : Enumerable.Range(0, items.Length);
			foreach (var index in indexes)
			{
				var context = new MultiInstanceSimulationContext(node.Id, index, items,
					loop.ElementVariable ?? loop.InputElement, loop.IsSequential);
				var contexts = AppendContext(pending.MultiInstanceContexts, context);
				foreach (var start in starts)
				{
					state.Queue.Enqueue(new PendingNode(start.Id, null, contexts));
				}
			}
		}
		else
		{
			foreach (var start in starts)
			{
				state.Queue.Enqueue(new PendingNode(start.Id, null, pending.MultiInstanceContexts));
			}
		}
	}

	private static void CompleteSimulatedScope(SimulationState state, SimulationNode node, PendingNode pending)
	{
		if (string.IsNullOrWhiteSpace(node.ParentSubprocessId))
		{
			state.ReachedRootEnd = true;
			if (node.EventDefinitionType == "terminate")
			{
				state.Queue.Clear();
			}
			return;
		}
		if (HasPendingInScope(state.Queue, state.BlockedJoins, state.Graph, node.ParentSubprocessId))
		{
			return;
		}
		var subprocess = state.Graph.GetNode(node.ParentSubprocessId);
		if (subprocess.IsEventSubprocess && string.IsNullOrWhiteSpace(subprocess.ParentSubprocessId)
			&& state.Graph.IsInterruptingEventSubprocess(subprocess.Id))
		{
			state.ReachedRootEnd = true;
		}
		var contexts = pending.MultiInstanceContexts;
		if (contexts is { Count: > 0 }
			&& string.Equals(contexts[^1].OwnerSubprocessId, node.ParentSubprocessId, StringComparison.Ordinal))
		{
			var completedIteration = contexts[^1];
			var parentContexts = contexts.Take(contexts.Count - 1).ToArray();
			if (completedIteration.IsSequential && completedIteration.Index + 1 < completedIteration.Items.Count)
			{
				var next = completedIteration with { Index = completedIteration.Index + 1 };
				var nextContexts = AppendContext(parentContexts, next);
				foreach (var start in state.Graph.SubprocessStartEvents(node.ParentSubprocessId))
				{
					state.Queue.Enqueue(new PendingNode(start.Id, null, nextContexts));
				}
				return;
			}
			contexts = parentContexts;
		}
		EnqueueFlows(state.Queue, state.Graph.Outgoing(node.ParentSubprocessId), contexts);
	}

	private static void StopAtStepLimit(SimulationState state)
	{
		state.Result.Completed = false;
		state.Result.Message = $"Simulation stopped after reaching MaxSteps={state.MaxSteps}.";
	}

	private void CompleteSimulation(SimulationState state)
	{
		if (state.BlockedJoins.Count > 0)
		{
			state.Result.Completed = false;
			state.Result.Message = "Simulation ended with unresolved joins: "
				+ string.Join(", ", state.BlockedJoins.Keys.Select(key => key.NodeId).OrderBy(key => key, StringComparer.Ordinal));
			return;
		}
		state.Result.Completed = state.ReachedRootEnd;
		state.Result.Message = state.ReachedRootEnd
			? "Simulation completed at a root end event."
			: "Simulation ended without reaching a root end event.";
		if (logger.IsEnabled(LogLevel.Information))
		{
			logger.LogInformation(
				"Deterministic simulation for {ProcessDefinitionId} completed={Completed} steps={StepCount}",
				state.Result.ProcessDefinitionId, state.Result.Completed, state.Result.Steps.Count);
		}
	}

	private static void ReleaseDynamicallyReadyJoins(
		Dictionary<JoinKey, HashSet<string>> blockedJoins,
		Queue<PendingNode> queue,
		SimulationGraph graph,
		SimulationResult result,
		Dictionary<string, object> variables)
	{
		bool released;
		do
		{
			released = false;
			foreach (var joinKey in blockedJoins.Keys.ToArray())
			{
				var node = graph.GetNode(joinKey.NodeId);
				var arrivals = blockedJoins[joinKey];
				var effectiveVariables = EffectiveVariables(variables, joinKey.MultiInstanceContexts);
				if (!CanReleaseJoin(node, arrivals, queue, graph, effectiveVariables, joinKey.ExecutionKey))
				{
					continue;
				}

				ReleaseJoin(joinKey, node, arrivals, blockedJoins, queue, graph, result,
					effectiveVariables, arrivals.FirstOrDefault());
				released = true;
			}
		} while (released);
	}

	private static bool CanReleaseJoin(
		SimulationNode node,
		HashSet<string> arrivals,
		Queue<PendingNode> queue,
		SimulationGraph graph,
		Dictionary<string, object> variables,
		string executionKey)
	{
		var incoming = graph.Incoming(node.Id);
		if (node.Type == "parallelGateway")
		{
			return incoming.All(flow => arrivals.Contains(flow.Id));
		}

		if (node.Type == "complexGateway"
			&& node.Attributes.TryGetValue("activationCondition", out var activationCondition))
		{
			var conditionVariables = new Dictionary<string, object>(variables, StringComparer.Ordinal)
			{
				["activationCount"] = arrivals.Count
			};
			if (BpmnConditionEvaluator.Evaluate(activationCondition, conditionVariables))
			{
				return true;
			}
		}

		return incoming
			.Where(flow => !arrivals.Contains(flow.Id))
			.All(flow => !graph.QueueCanStillReachFlow(queue, flow, executionKey));
	}

	private static void ReleaseJoin(
		JoinKey joinKey,
		SimulationNode node,
		HashSet<string> arrivals,
		Dictionary<JoinKey, HashSet<string>> blockedJoins,
		Queue<PendingNode> queue,
		SimulationGraph graph,
		SimulationResult result,
		Dictionary<string, object> variables,
		string? incomingFlowId)
	{
		AddStep(result, node, variables, incomingFlowId);
		blockedJoins.Remove(joinKey);
		arrivals.Clear();
		if (node.Type == "complexGateway")
		{
			EnqueueFlows(queue, SelectConditionalFlows(node, graph.Outgoing(node.Id), variables),
				joinKey.MultiInstanceContexts);
		}
		else
		{
			EnqueueFlows(queue, graph.Outgoing(node.Id), joinKey.MultiInstanceContexts);
		}
	}

	private static BpmnSequenceFlow SelectExclusiveFlow(
		SimulationNode gateway,
		IReadOnlyList<BpmnSequenceFlow> outgoing,
		Dictionary<string, object> variables)
	{
		EnsureOutgoing(gateway, outgoing);
		var defaults = outgoing.Where(flow => flow.IsDefault).ToArray();
		if (defaults.Length > 1)
		{
			throw new InvalidOperationException($"Gateway '{gateway.Id}' declares multiple default flows.");
		}

		var selected = outgoing
			.Where(flow => !flow.IsDefault)
			.FirstOrDefault(flow => string.IsNullOrWhiteSpace(flow.ConditionExpression)
									|| BpmnConditionEvaluator.Evaluate(flow, variables));
		return selected
			   ?? defaults.SingleOrDefault()
			   ?? throw new InvalidOperationException(
				   $"Exclusive gateway '{gateway.Id}' has no matching condition and no default flow.");
	}

	private static BpmnSequenceFlow[] SelectConditionalFlows(
		SimulationNode gateway,
		IReadOnlyList<BpmnSequenceFlow> outgoing,
		Dictionary<string, object> variables)
	{
		EnsureOutgoing(gateway, outgoing);
		var defaults = outgoing.Where(flow => flow.IsDefault).ToArray();
		if (defaults.Length > 1)
		{
			throw new InvalidOperationException($"Gateway '{gateway.Id}' declares multiple default flows.");
		}

		var selected = outgoing
			.Where(flow => !flow.IsDefault && (string.IsNullOrWhiteSpace(flow.ConditionExpression)
						   || BpmnConditionEvaluator.Evaluate(flow, variables)))
			.ToArray();
		if (selected.Length > 0)
		{
			return selected;
		}

		if (defaults.Length == 1)
		{
			return defaults;
		}

		throw new InvalidOperationException(
			$"Gateway '{gateway.Id}' has no matching condition and no default flow.");
	}

	private static BpmnSequenceFlow SelectEventFlow(
		SimulationNode gateway,
		List<BpmnSequenceFlow> outgoing,
		Dictionary<string, string> selections)
	{
		EnsureOutgoing(gateway, outgoing);
		if (outgoing.Count == 1)
		{
			return outgoing[0];
		}

		if (!selections.TryGetValue(gateway.Id, out var selection) || string.IsNullOrWhiteSpace(selection))
		{
			throw new InvalidOperationException(
				$"Event-based gateway '{gateway.Id}' requires EventSelections['{gateway.Id}'] "
				+ "with an outgoing flow id or catch-event id.");
		}

		return outgoing.SingleOrDefault(flow =>
				   string.Equals(flow.Id, selection, StringComparison.Ordinal)
				   || string.Equals(flow.TargetRef, selection, StringComparison.Ordinal))
			   ?? throw new InvalidOperationException(
				   $"Event selection '{selection}' is not an outgoing branch of gateway '{gateway.Id}'.");
	}

	private static void EnsureOutgoing(SimulationNode gateway, IReadOnlyList<BpmnSequenceFlow> outgoing)
	{
		if (outgoing.Count == 0)
		{
			throw new InvalidOperationException($"Gateway '{gateway.Id}' has no outgoing sequence flow.");
		}
	}

	private static void EnqueueFlows(
		Queue<PendingNode> queue,
		IEnumerable<BpmnSequenceFlow> flows,
		IReadOnlyList<MultiInstanceSimulationContext>? multiInstanceContexts = null)
	{
		foreach (var flow in flows)
		{
			queue.Enqueue(new PendingNode(flow.TargetRef, flow.Id, multiInstanceContexts));
		}
	}

	private static Dictionary<string, object> EffectiveVariables(
		Dictionary<string, object> processVariables,
		IReadOnlyList<MultiInstanceSimulationContext>? contexts)
	{
		var effective = new Dictionary<string, object>(processVariables, StringComparer.Ordinal);
		if (contexts is null)
		{
			return effective;
		}

		foreach (var context in contexts)
		{
			effective["loopCounter"] = context.Index;
			effective["nrOfInstances"] = context.Items.Count;
			effective["nrOfCompletedInstances"] = context.Index;
			effective["nrOfActiveInstances"] = context.IsSequential
				? 1
				: context.Items.Count - context.Index;
			if (!string.IsNullOrWhiteSpace(context.ElementVariable))
			{
				effective[context.ElementVariable] = context.Items[context.Index] ?? new object();
			}
		}
		return effective;
	}

	private static object?[] ResolveMultiInstanceItems(
		MultiInstanceLoopCharacteristics loop,
		Dictionary<string, object> variables)
	{
		if (!string.IsNullOrWhiteSpace(loop.Collection))
		{
			var variableName = loop.Collection.Trim();
			if (variableName.StartsWith("${", StringComparison.Ordinal) && variableName.EndsWith('}'))
			{
				variableName = variableName[2..^1].Trim();
			}
			else if (variableName.StartsWith('='))
			{
				variableName = variableName[1..].Trim();
			}

			if (!variables.TryGetValue(variableName, out var collection))
			{
				throw new InvalidOperationException(
					$"Multi-instance collection variable '{variableName}' was not supplied.");
			}

			if (collection is string || collection is IDictionary || collection is not IEnumerable enumerable)
			{
				throw new InvalidOperationException(
					$"Multi-instance collection variable '{variableName}' is not an array or enumerable value.");
			}

			return [.. enumerable.Cast<object?>().Select(BpmnConditionEvaluator.NormalizeJsonValue)];
		}

		var cardinality = loop.LoopCardinality ?? 1;
		if (cardinality < 0)
		{
			throw new InvalidOperationException("Multi-instance loop cardinality cannot be negative.");
		}

		return [.. Enumerable.Range(0, cardinality).Select(index => (object?)index)];
	}

	private static MultiInstanceSimulationContext[] AppendContext(
		IReadOnlyList<MultiInstanceSimulationContext>? contexts,
		MultiInstanceSimulationContext context) =>
		contexts is null ? [context] : [.. contexts, context];

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
		Dictionary<JoinKey, HashSet<string>> blockedJoins,
		SimulationGraph graph,
		string subprocessId) =>
		queue.Any(pending => graph.IsInScope(pending.NodeId, subprocessId))
		|| blockedJoins.Keys.Any(key => graph.IsInScope(key.NodeId, subprocessId));

	private sealed record PendingNode(
		string NodeId,
		string? IncomingFlowId,
		IReadOnlyList<MultiInstanceSimulationContext>? MultiInstanceContexts = null)
	{
		public string ExecutionKey => MultiInstanceContexts is { Count: > 0 }
			? string.Join("/", MultiInstanceContexts.Select(context =>
				$"{context.OwnerSubprocessId}:{context.Index}"))
			: string.Empty;
	}

	private sealed record JoinKey(
		string NodeId,
		string ExecutionKey,
		IReadOnlyList<MultiInstanceSimulationContext>? MultiInstanceContexts);

	private sealed record MultiInstanceSimulationContext(
		string OwnerSubprocessId,
		int Index,
		IReadOnlyList<object?> Items,
		string? ElementVariable,
		bool IsSequential);

	private sealed record SimulationNode(
		string Id,
		string Type,
		string Name,
		string? ParentSubprocessId,
		string? EventDefinitionType,
		IReadOnlyDictionary<string, string> Attributes,
		bool IsEventSubprocess = false,
		LoopCharacteristics? Loop = null)
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
			RootStartEvents = [.. nodes.Values
				.Where(node => node.Type == "startEvent" && node.ParentSubprocessId is null)
				.OrderBy(node => node.Id, StringComparer.Ordinal)];
		}

		public SimulationNode[] RootStartEvents { get; }

		public static SimulationGraph Create(BpmnModel model)
		{
			var nodes = CreateNodes(model);
			var flows = (model.SequenceFlows ?? []).ToArray();
			var invalidFlow = flows.FirstOrDefault(flow => !nodes.ContainsKey(flow.SourceRef) || !nodes.ContainsKey(flow.TargetRef));
			if (invalidFlow is not null)
			{
				throw new InvalidOperationException($"Sequence flow '{invalidFlow.Id}' references an unknown source or target.");
			}
			var outgoing = flows.GroupBy(flow => flow.SourceRef, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
			var incoming = flows.GroupBy(flow => flow.TargetRef, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
			foreach (var node in nodes.Values.Where(node => node.Type is "parallelGateway" or "inclusiveGateway" or "complexGateway"))
			{
				node.IsJoin = incoming.GetValueOrDefault(node.Id)?.Count > 1;
			}

			return new SimulationGraph(nodes, outgoing, incoming);
		}

		private static Dictionary<string, SimulationNode> CreateNodes(BpmnModel model)
		{
			var nodes = new Dictionary<string, SimulationNode>(StringComparer.Ordinal);
			foreach (var @event in model.Events ?? [])
			{
				Add(nodes, new SimulationNode(
					@event.Id,
					@event.Type,
					string.IsNullOrWhiteSpace(@event.Name) ? @event.Id : @event.Name,
					@event.SubprocessId,
					@event.EventDefinitionType,
					@event.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal)));
			}

			foreach (var task in model.Tasks ?? [])
			{
				Add(nodes, new SimulationNode(
					task.Id,
					task.Type,
					string.IsNullOrWhiteSpace(task.Name) ? task.Id : task.Name,
					task.SubprocessId,
					null,
					task.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal)));
			}

			foreach (var gateway in model.Gateways ?? [])
			{
				Add(nodes, new SimulationNode(
					gateway.Id,
					gateway.Type,
					gateway.ExtensionAttributes?.GetValueOrDefault("name") ?? gateway.Id,
					gateway.SubprocessId,
					null,
					gateway.ExtensionAttributes ?? new Dictionary<string, string>(StringComparer.Ordinal)));
			}

			foreach (var subprocess in model.Subprocesses ?? [])
			{
				Add(nodes, new SimulationNode(
					subprocess.Id,
					subprocess.IsTransaction ? "transaction" : "subProcess",
					subprocess.ExtensionAttributes?.GetValueOrDefault("name") ?? subprocess.Id,
					subprocess.SubprocessId,
					null,
					subprocess.ExtensionAttributes ?? new Dictionary<string, string>(StringComparer.Ordinal),
					subprocess.IsEventSubprocess,
					subprocess.Loop));
			}

			return nodes;
		}

		public void EnsureQualifiedModel()
		{
			if (RootStartEvents.Length == 0)
			{
				throw new InvalidOperationException("The BPMN process has no root start event.");
			}
		}

		public SimulationNode[] SelectedEventSubprocessStarts(
			IReadOnlyDictionary<string, string> selections) =>
			[.. _nodes.Values
				.Where(node => node.IsEventSubprocess)
				.OrderBy(node => node.Id, StringComparer.Ordinal)
				.SelectMany(eventSubprocess => SubprocessStartEvents(eventSubprocess.Id)
					.Where(start => IsSelected(eventSubprocess, start, selections)))];

		public static bool IsInterruptingEventSubprocessStart(SimulationNode start) =>
			start.EventDefinitionType == "error"
			|| !start.Attributes.TryGetValue("isInterrupting", out var value)
			|| !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

		public bool IsInterruptingEventSubprocess(string subprocessId) =>
			SubprocessStartEvents(subprocessId).Any(IsInterruptingEventSubprocessStart);

		private static bool IsSelected(
			SimulationNode eventSubprocess,
			SimulationNode start,
			IReadOnlyDictionary<string, string> selections)
		{
			if (!selections.TryGetValue(eventSubprocess.Id, out var selection)
				&& !selections.TryGetValue(start.Id, out selection))
			{
				return false;
			}

			return !string.Equals(selection, "false", StringComparison.OrdinalIgnoreCase)
				   && !string.Equals(selection, "ignore", StringComparison.OrdinalIgnoreCase);
		}

		public SimulationNode GetNode(string id) =>
			_nodes.TryGetValue(id, out var node)
				? node
				: throw new InvalidOperationException($"Flow node '{id}' is not defined.");

		public List<BpmnSequenceFlow> Outgoing(string nodeId) =>
			_outgoing.GetValueOrDefault(nodeId) ?? [];

		public List<BpmnSequenceFlow> Incoming(string nodeId) =>
			_incoming.GetValueOrDefault(nodeId) ?? [];

		public SimulationNode[] SubprocessStartEvents(string subprocessId) =>
			[.. _nodes.Values
				.Where(node => node.Type == "startEvent"
							   && string.Equals(node.ParentSubprocessId, subprocessId, StringComparison.Ordinal))
				.OrderBy(node => node.Id, StringComparer.Ordinal)];

		public bool IsInScope(string nodeId, string subprocessId)
		{
			var current = GetNode(nodeId).ParentSubprocessId;
			while (!string.IsNullOrWhiteSpace(current))
			{
				if (string.Equals(current, subprocessId, StringComparison.Ordinal))
				{
					return true;
				}

				current = GetNode(current).ParentSubprocessId;
			}
			return false;
		}

		public bool QueueCanStillReachFlow(
			Queue<PendingNode> queue,
			BpmnSequenceFlow targetFlow,
			string executionKey) =>
			queue.Any(pending =>
				string.Equals(pending.ExecutionKey, executionKey, StringComparison.Ordinal)
				&& (string.Equals(pending.IncomingFlowId, targetFlow.Id, StringComparison.Ordinal)
					|| CanReach(pending.NodeId, targetFlow.SourceRef)));

		private bool CanReach(string startNodeId, string targetNodeId)
		{
			var pending = new Stack<string>();
			var visited = new HashSet<string>(StringComparer.Ordinal);
			pending.Push(startNodeId);
			while (pending.Count > 0)
			{
				var current = pending.Pop();
				if (!visited.Add(current))
				{
					continue;
				}

				if (string.Equals(current, targetNodeId, StringComparison.Ordinal))
				{
					return true;
				}

				foreach (var flow in Outgoing(current))
				{
					pending.Push(flow.TargetRef);
				}
			}
			return false;
		}

		private static void Add(Dictionary<string, SimulationNode> nodes, SimulationNode node)
		{
			if (string.IsNullOrWhiteSpace(node.Id) || !nodes.TryAdd(node.Id, node))
			{
				throw new InvalidOperationException($"Duplicate or empty BPMN flow-node id '{node.Id}'.");
			}
		}
	}
}
