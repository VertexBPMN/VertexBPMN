using System.Globalization;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Execution;

public sealed partial class PersistentProcessExecutionRuntime
{
	private Task AdvanceNodeAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken) => node.Kind switch
		{
			"startEvent" or "boundaryEvent" => AdvancePassThroughEventAsync(node, pending, model, queue),
			"exclusiveGateway" => AdvanceExclusiveGatewayAsync(instance, node, pending, model, queue, cancellationToken),
			"inclusiveGateway" => AdvanceInclusiveGatewayAsync(instance, node, pending, model, queue, cancellationToken),
			"parallelGateway" => AdvanceParallelGatewayAsync(instance, node, pending, model, queue, cancellationToken),
			"eventBasedGateway" => AdvanceEventGatewayAsync(instance, node, pending, model, queue, cancellationToken),
			"complexGateway" => AdvanceComplexGatewayAsync(instance, node, pending, model, queue, cancellationToken),
			"serviceTask" => AdvanceServiceTaskAsync(instance, node, pending, model, queue, cancellationToken),
			"userTask" => AdvanceUserTaskAsync(instance, node, pending, model, cancellationToken),
			"subProcess" or "transaction" => AdvanceSubprocessAsync(instance, node, pending, model, queue, cancellationToken),
			"intermediateCatchEvent" => AdvanceCatchEventAsync(instance, node, pending, model, cancellationToken),
			"endEvent" => AdvanceEndEventAsync(instance, node, pending, model, queue, cancellationToken),
			"intermediateThrowEvent" => AdvanceThrowEventAsync(instance, node, pending, model, queue, cancellationToken),
			"task" or "manualTask" or "sendTask" or "receiveTask" => AdvanceAutomaticTaskAsync(instance, node, pending, model, queue, cancellationToken),
			"businessRuleTask" => AdvanceDecisionTaskAsync(instance, node, pending, model, queue, cancellationToken),
			"callActivity" => AdvanceCallActivityAsync(instance, node, pending, cancellationToken),
			"scriptTask" => AdvanceScriptTaskAsync(instance, node, pending, model, queue, cancellationToken),
			_ => throw new NotSupportedException($"Flow node type '{node.Kind}' is not supported by the production subset.")
		};

	private static Task AdvancePassThroughEventAsync(ExecutionNode node, PendingNode pending, ExecutionModel model, Queue<PendingNode> queue)
	{
		Enqueue(queue, model.Outgoing(node.Id), pending);
		return Task.CompletedTask;
	}

	private async Task AdvanceExclusiveGatewayAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		var exclusiveFlow = SelectExclusiveFlow(node, model.Outgoing(node.Id), instance.Variables);
		if (exclusiveFlow is null)
		{
			await SuspendWithIncidentAsync(
				instance,
				node.Id,
				$"Exclusive gateway '{node.Id}' has no matching condition and no default flow.",
				cancellationToken);
			return;
		}
		queue.Enqueue(WithExecutionContext(exclusiveFlow, pending));
		AddHistory(instance, "EXCLUSIVE_GATEWAY_SELECTED", node.Id, new { exclusiveFlow.FlowId });
	}

	private async Task AdvanceInclusiveGatewayAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		if (model.IncomingCount(node.Id) > 1 && model.OutgoingCount(node.Id) <= 1)
		{
			if (await ArriveInclusiveJoinAsync(
					instance, node, pending, model, queue, cancellationToken))
			{
				Enqueue(queue, model.Outgoing(node.Id));
			}

			return;
		}
		var inclusiveFlows = SelectInclusiveFlows(node, model.Outgoing(node.Id), instance.Variables);
		if (inclusiveFlows.Length == 0)
		{
			await SuspendWithIncidentAsync(
				instance,
				node.Id,
				$"Inclusive gateway '{node.Id}' has no matching condition and no default flow.",
				cancellationToken);
			return;
		}
		Enqueue(queue, inclusiveFlows, pending);
		AddHistory(instance, "INCLUSIVE_GATEWAY_SELECTED", node.Id,
			new { flowIds = inclusiveFlows.Select(flow => flow.FlowId).ToArray() });
	}

	private async Task AdvanceParallelGatewayAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		if (model.IncomingCount(node.Id) > 1 && model.OutgoingCount(node.Id) <= 1)
		{
			if (await ArriveGatewayJoinAsync(instance, node, pending, model, cancellationToken))
			{
				Enqueue(queue, model.Outgoing(node.Id), pending);
			}
		}
		else
		{
			Enqueue(queue, model.Outgoing(node.Id), pending);
		}
	}

	private async Task AdvanceEventGatewayAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		var eventBranches = model.Outgoing(node.Id).ToArray();
		if (eventBranches.Length == 0
			|| eventBranches.Any(branch => !model.Nodes.TryGetValue(branch.NodeId, out var target)
										   || target.Kind != "intermediateCatchEvent"
										   || target.EventType is not ("Message" or "Signal" or "Timer")))
		{
			await SuspendWithIncidentAsync(
				instance,
				node.Id,
				$"Event-based gateway '{node.Id}' must target message, signal, or timer catch events.",
				cancellationToken);
			return;
		}
		Enqueue(queue, eventBranches, pending);
		AddHistory(instance, "EVENT_GATEWAY_ACTIVATED", node.Id,
			new { branches = eventBranches.Select(branch => branch.NodeId).ToArray() });
	}

	private async Task AdvanceComplexGatewayAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		if (model.IncomingCount(node.Id) > 1 && model.OutgoingCount(node.Id) <= 1)
		{
			if (await ArriveGatewayJoinAsync(instance, node, pending, model, cancellationToken))
			{
				Enqueue(queue, model.Outgoing(node.Id), pending);
			}

			return;
		}
		var complexFlows = SelectInclusiveFlows(node, model.Outgoing(node.Id), instance.Variables);
		if (complexFlows.Length == 0)
		{
			await SuspendWithIncidentAsync(
				instance,
				node.Id,
				$"Complex gateway '{node.Id}' did not activate an outgoing flow.",
				cancellationToken);
			return;
		}
		Enqueue(queue, complexFlows, pending);
		AddHistory(instance, "COMPLEX_GATEWAY_ACTIVATED", node.Id,
			new { flowIds = complexFlows.Select(flow => flow.FlowId).ToArray() });
	}

	private async Task AdvanceServiceTaskAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		if (ExternalTaskDefinition.FromAttributes(node.Attributes) is { } externalDefinition)
		{
			if (node.Attributes.ContainsKey("standardLoop"))
			{
				var locals = pending.LocalVariables is null ? new Dictionary<string, object>(StringComparer.Ordinal)
					: new Dictionary<string, object>(pending.LocalVariables, StringComparer.Ordinal);
				var enteringLoop = !locals.ContainsKey("loopCounter");
				if (enteringLoop)
				{
					locals["loopCounter"] = 0;
				}

				var maximum = node.Attributes.GetValueOrDefault("standardLoopMaximum");
				var limit = maximum is null ? int.MaxValue : int.Parse(maximum, CultureInfo.InvariantCulture);
				if (limit < 0)
				{
					throw new InvalidOperationException("external_task_invalid_loop_limit");
				}

				var condition = node.Attributes.GetValueOrDefault("standardLoopCondition");
				if (limit == 0 || (enteringLoop && node.Attributes.GetValueOrDefault("standardLoopTestBefore") == "true"
					&& !string.IsNullOrWhiteSpace(condition)
					&& !BpmnConditionEvaluator.Evaluate(condition, CreateActivityVariables(instance.Variables, locals))))
				{
					Enqueue(queue, model.Outgoing(node.Id), pending);
					return;
				}
				pending = pending with { LocalVariables = locals };
			}
			try
			{
				await CreateExternalTaskWaitAsync(instance, node, pending, model, externalDefinition, cancellationToken);
			}
			catch (InvalidOperationException error) when (error.Message is
				"external_task_input_missing" or "external_task_input_schema_invalid"
				or "external_task_input_not_allowed" or "external_task_input_too_large"
				or "external_task_contract_unavailable" or "external_task_contract_limits_exceeded")
			{
				// Only fixed validation codes are audited; never serialize rejected inputs
				// or turn persistence/concurrency failures into successful scheduling.
				await SuspendWithIncidentAsync(instance, node.Id, error.Message, cancellationToken, pending);
			}
			return;
		}
		await ExecuteServiceTaskAsync(instance, node, pending.LocalVariables, cancellationToken);
		if (instance.Status == ProcessInstanceStatus.Running)
		{
			await RegisterCompensationAsync(instance, node, model, cancellationToken);
			await CompleteActivityAsync(instance, node, pending, model, queue, cancellationToken);
		}
	}

	private async Task AdvanceUserTaskAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending, ExecutionModel model, CancellationToken cancellationToken)
	{
		await CreateUserTaskWaitAsync(instance, node, pending, model, cancellationToken);
	}

	private async Task AdvanceSubprocessAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		var subprocessStarts = model.SubprocessStartNodes(node.Id).ToArray();
		if (subprocessStarts.Length == 0)
		{
			// An empty embedded subprocess represents one successful execution.
			await RegisterCompensationAsync(instance, node, model, cancellationToken);
			await CompleteActivityAsync(instance, node, pending, model, queue, cancellationToken);
		}
		else
		{
			var scopedLocals = pending.LocalVariables is null
				? new Dictionary<string, object>(StringComparer.Ordinal)
				: new Dictionary<string, object>(pending.LocalVariables, StringComparer.Ordinal);
			scopedLocals["$vertex.scopeExecution." + node.Id] = Guid.NewGuid().ToString();
			Enqueue(queue, subprocessStarts, pending with { LocalVariables = scopedLocals });
			if (model.Nodes.Values.Any(candidate => model.IsInScope(candidate.Id, node.Id)
				&& ExternalTaskDefinition.FromAttributes(candidate.Attributes) is not null))
			{
				await RegisterExternalBoundariesAsync(instance, node, model, cancellationToken);
			}

			await ActivateEventSubprocessesAsync(instance, model, node.Id, cancellationToken);
		}
	}

	private async Task AdvanceCatchEventAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending, ExecutionModel model, CancellationToken cancellationToken)
	{
		_ = await CreateEventWaitAsync(instance, node, pending, model, cancellationToken);
	}

	private async Task AdvanceEndEventAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		var compensationCompleted = pending.LocalVariables?.ContainsKey(CompensationCompletedVariable) == true;
		if (node.EventType == "Compensation" && !compensationCompleted)
		{
			if (await TriggerCompensationAsync(instance, node, model, queue, cancellationToken))
			{
				return;
			}
		}
		else if (node.EventType == "Terminate")
		{
			await TerminateScopeAsync(instance, node, model, queue, cancellationToken);
		}
		else if (node.EventType is "Error" or "Escalation" or "Cancel")
		{
			if (node.EventType == "Cancel" && !compensationCompleted
				&& await TriggerCompensationAsync(instance, node, model, queue, cancellationToken))
			{
				return;
			}

			if (await HandleScopedThrowAsync(instance, node, model, queue, cancellationToken)
				== ScopedThrowResult.NotCaught)
			{
				await SuspendWithIncidentAsync(
					instance,
					node.Id,
					$"{node.EventType} end event '{node.Id}' has no matching boundary handler.",
					cancellationToken);
			}
			return;
		}
		AddHistory(instance, "END_EVENT_REACHED", node.Id, new { node.EventType });
		await CompleteEndEventScopeAsync(instance, node, pending, model, queue, cancellationToken);
	}

	private async Task CompleteEndEventScopeAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(node.ParentSubprocessId)
			&& !await HasOtherActiveScopeWorkAsync(instance.Id, node.ParentSubprocessId, pending, model, queue, cancellationToken))
		{
			var completedScope = model.Nodes[node.ParentSubprocessId];
			if (completedScope.IsEventSubprocess)
			{
				await CompleteEventSubprocessAsync(
					instance, completedScope, model, queue, pending, cancellationToken);
				return;
			}

			await RegisterCompensationAsync(instance, completedScope, model, cancellationToken);

			if (pending.LocalVariables?.ContainsKey(CompensationBatchVariable) == true)
			{
				await ContinueCompensationAsync(
					instance, pending.LocalVariables, model, queue, cancellationToken);
				return;
			}

			if (pending.MultiInstanceExecutionId.HasValue
				&& string.Equals(pending.MultiInstanceActivityId, node.ParentSubprocessId, StringComparison.Ordinal))
			{
				var owner = model.Nodes[node.ParentSubprocessId];
				await CompleteMultiInstanceIterationAsync(
					instance,
					owner,
					pending.MultiInstanceExecutionId.Value,
					pending.MultiInstanceIndex.GetValueOrDefault(),
					model,
					queue,
					cancellationToken);
			}
			else
			{
				Enqueue(queue, model.Outgoing(node.ParentSubprocessId), pending);
			}
		}
	}

	private async Task AdvanceThrowEventAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		var intermediateCompensationCompleted =
			pending.LocalVariables?.ContainsKey(CompensationCompletedVariable) == true;
		if (node.EventType == "Compensation")
		{
			if (!intermediateCompensationCompleted
				&& await TriggerCompensationAsync(instance, node, model, queue, cancellationToken))
			{
				return;
			}
		}
		else if (node.EventType is "Error" or "Escalation" or "Cancel")
		{
			if (node.EventType == "Cancel" && !intermediateCompensationCompleted
				&& await TriggerCompensationAsync(instance, node, model, queue, cancellationToken))
			{
				return;
			}

			var catchResult = await HandleScopedThrowAsync(
				instance, node, model, queue, cancellationToken);
			if (catchResult == ScopedThrowResult.NotCaught)
			{
				await SuspendWithIncidentAsync(
					instance,
					node.Id,
					$"{node.EventType} throw event '{node.Id}' has no matching boundary handler.",
					cancellationToken);
			}
			else if (catchResult == ScopedThrowResult.NonInterrupting)
			{
				Enqueue(queue, model.Outgoing(node.Id), pending);
			}

			return;
		}
		else
		{
			throw new NotSupportedException($"Throw event '{node.Id}' has unsupported definition '{node.EventType}'.");
		}
		Enqueue(queue, model.Outgoing(node.Id), pending);
	}

	private async Task AdvanceAutomaticTaskAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		await RegisterCompensationAsync(instance, node, model, cancellationToken);
		await CompleteActivityAsync(instance, node, pending, model, queue, cancellationToken);
	}

	private async Task AdvanceDecisionTaskAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		await ExecuteBusinessRuleTaskAsync(instance, node, pending.LocalVariables, cancellationToken);
		if (instance.Status == ProcessInstanceStatus.Running)
		{
			await RegisterCompensationAsync(instance, node, model, cancellationToken);
			await CompleteActivityAsync(instance, node, pending, model, queue, cancellationToken);
		}
	}

	private async Task AdvanceCallActivityAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending, CancellationToken cancellationToken)
	{
		await StartCallActivityAsync(instance, node, pending, cancellationToken);
	}

	private async Task AdvanceScriptTaskAsync(ProcessInstance instance, ExecutionNode node, PendingNode pending,
		ExecutionModel model, Queue<PendingNode> queue, CancellationToken cancellationToken)
	{
		await ExecuteScriptTaskAsync(instance, node, pending, cancellationToken);
		if (instance.Status == ProcessInstanceStatus.Running)
		{
			await RegisterCompensationAsync(instance, node, model, cancellationToken);
			await CompleteActivityAsync(instance, node, pending, model, queue, cancellationToken);
		}
	}
}
