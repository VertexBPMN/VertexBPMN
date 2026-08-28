using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Jint;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Engine.Execution;

/// <summary>
/// Transactional BPMN subset runtime used by every public execution API.
/// Its wait states are database rows; no in-memory trace is authoritative.
/// </summary>
public sealed class PersistentProcessExecutionRuntime : IProcessExecutionRuntime
{
    private const string ActiveSubscription = "Active";
    private const string ScheduledJob = "Scheduled";
    private const string EventGatewayVariable = "$vertex.eventGateway";
    private const string TaskIdVariable = "$vertex.taskId";
    private const string MultiInstanceIdVariable = "$vertex.multiInstanceId";
    private const string MultiInstanceIndexVariable = "$vertex.multiInstanceIndex";
    private const string MultiInstanceActivityIdVariable = "$vertex.multiInstanceActivityId";
    private const string EventSubprocessIdVariable = "$vertex.eventSubprocessId";
    private const string EventSubprocessInterruptingVariable = "$vertex.eventSubprocessInterrupting";
    private const string CompensationScopeVariable = "$vertex.compensationScope";
    private const string CompensationOrderVariable = "$vertex.compensationOrder";
    private const string CompensationBatchVariable = "$vertex.compensationBatch";
    private const string CompensationThrowEventVariable = "$vertex.compensationThrowEvent";
    private const string CompensationSubscriptionVariable = "$vertex.compensationSubscription";
    private const string CompensationCompletedVariable = "$vertex.compensationCompleted";
    private const string RootCompensationScope = "$vertex.root";
    private const int MaxAutomaticSteps = 10_000;

    private readonly BpmnDbContext _db;
    private readonly IServiceTaskRegistry _serviceTasks;
    private readonly IDecisionService _decisions;
    private readonly ILogger<PersistentProcessExecutionRuntime> _logger;

    public PersistentProcessExecutionRuntime(
        BpmnDbContext db,
        IServiceTaskRegistry serviceTasks,
        IDecisionService decisions,
        ILogger<PersistentProcessExecutionRuntime> logger)
    {
        _db = db;
        _serviceTasks = serviceTasks;
        _decisions = decisions;
        _logger = logger;
    }

    public async ValueTask<ProcessInstance> StartAsync(
        ProcessDefinition definition,
        IDictionary<string, object>? variables,
        string? businessKey,
        string? tenantId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        EnsureTenant(definition.TenantId, tenantId);

        var duplicate = await FindInboxAsync("start", idempotencyKey, tenantId, cancellationToken);
        if (duplicate?.Result is { Length: > 0 } result && Guid.TryParse(result, out var existingId))
        {
            return await _db.ProcessInstances.AsNoTracking()
                       .SingleAsync(instance => instance.Id == existingId, cancellationToken);
        }

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var inbox = AddInbox("start", idempotencyKey, tenantId);
        var duplicateClaim = await ClaimInboxAsync(
            inbox, transaction, "start", idempotencyKey, tenantId, cancellationToken);
        if (duplicateClaim?.Result is { Length: > 0 } claimedResult
            && Guid.TryParse(claimedResult, out var claimedInstanceId))
            return await _db.ProcessInstances.AsNoTracking()
                .SingleAsync(instance => instance.Id == claimedInstanceId, cancellationToken);
        var now = DateTime.UtcNow;
        var instance = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            ProcessDefinitionId = definition.Id,
            ProcessId = definition.Key,
            BusinessKey = businessKey,
            TenantId = tenantId,
            StartedAt = now,
            CreatedAt = now,
            LastModified = now,
            State = "Running",
            Status = ProcessInstanceStatus.Running,
            Variables = variables is null
                ? []
                : new Dictionary<string, object>(variables, StringComparer.Ordinal),
            Revision = 1
        };

        _db.ProcessInstances.Add(instance);
        AddHistory(instance, "PROCESS_STARTED", "", new { definition.Key, definition.Version });
        AddOutbox(instance, "ProcessStarted", new { definition.Key, definition.Version, businessKey });

        var model = ExecutionModel.Parse(definition.BpmnXml, definition.Key);
        var startNodes = model.Nodes.Values
            .Where(node => node.Kind == "startEvent" && string.IsNullOrEmpty(node.ParentSubprocessId))
            .Select(node => new PendingNode(node.Id, null))
            .ToArray();
        if (startNodes.Length == 0)
            throw new InvalidOperationException($"Process '{definition.Key}' has no executable start event.");

        await AdvanceAsync(instance, model, startNodes, cancellationToken);
        await ActivateEventSubprocessesAsync(instance, model, null, cancellationToken);
        await FinalizeTransitionAsync(instance, cancellationToken);
        CompleteInbox(inbox, instance.Id.ToString());
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return instance;
    }

    public async ValueTask<MessageCorrelationResult> CorrelateMessageAsync(
        string messageName,
        Guid? processInstanceId,
        IDictionary<string, object>? variables,
        string? tenantId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageName);
        var duplicate = await FindInboxAsync("message", idempotencyKey, tenantId, cancellationToken);
        if (duplicate?.Result is { Length: > 0 })
            return JsonSerializer.Deserialize<MessageCorrelationResult>(duplicate.Result)!;

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var inbox = AddInbox("message", idempotencyKey, tenantId);
        var duplicateClaim = await ClaimInboxAsync(
            inbox, transaction, "message", idempotencyKey, tenantId, cancellationToken);
        if (duplicateClaim?.Result is { Length: > 0 })
            return JsonSerializer.Deserialize<MessageCorrelationResult>(duplicateClaim.Result)!;
        var query = _db.EventSubscriptions
            .Where(subscription => subscription.EventType == "Message"
                                   && subscription.EventName == messageName
                                   && subscription.State == ActiveSubscription
                                   && subscription.TenantId == tenantId);
        if (processInstanceId.HasValue)
            query = query.Where(subscription => subscription.ProcessInstanceId == processInstanceId.Value);

        var subscription = await query.OrderBy(subscription => subscription.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (subscription is null)
        {
            var notFound = new MessageCorrelationResult(
                "not_found", "", processInstanceId?.ToString() ?? "", "");
            CompleteInbox(inbox, JsonSerializer.Serialize(notFound));
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return notFound;
        }

        var instance = await LoadActiveInstanceAsync(subscription.ProcessInstanceId, tenantId, cancellationToken);
        var definition = await _db.ProcessDefinitions.AsNoTracking()
            .SingleAsync(item => item.Id == instance.ProcessDefinitionId, cancellationToken);
        MergeVariables(instance, variables);
        await ConsumeSubscriptionAsync(instance, subscription, ExecutionModel.Parse(definition.BpmnXml, definition.Key), cancellationToken);
        await FinalizeTransitionAsync(instance, cancellationToken);

        var correlated = new MessageCorrelationResult(
            "correlated",
            subscription.ExecutionTokenId.ToString(),
            instance.Id.ToString(),
            instance.ProcessDefinitionId.ToString());
        CompleteInbox(inbox, JsonSerializer.Serialize(correlated));
        AddOutbox(instance, "MessageCorrelated", new { messageName, subscription.ActivityId });
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return correlated;
    }

    public async ValueTask BroadcastSignalAsync(
        string signalName,
        IDictionary<string, object>? variables,
        string? tenantId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);
        if (await FindInboxAsync("signal", idempotencyKey, tenantId, cancellationToken) is not null)
            return;

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var inbox = AddInbox("signal", idempotencyKey, tenantId);
        if (await ClaimInboxAsync(inbox, transaction, "signal", idempotencyKey, tenantId, cancellationToken) is not null)
            return;
        var subscriptions = await _db.EventSubscriptions
            .Where(subscription => subscription.EventType == "Signal"
                                   && subscription.EventName == signalName
                                   && subscription.State == ActiveSubscription
                                   && subscription.TenantId == tenantId)
            .OrderBy(subscription => subscription.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var group in subscriptions.GroupBy(subscription => subscription.ProcessInstanceId))
        {
            var instance = await LoadActiveInstanceAsync(group.Key, tenantId, cancellationToken);
            var definition = await _db.ProcessDefinitions.AsNoTracking()
                .SingleAsync(item => item.Id == instance.ProcessDefinitionId, cancellationToken);
            var model = ExecutionModel.Parse(definition.BpmnXml, definition.Key);
            MergeVariables(instance, variables);
            foreach (var subscription in group)
                await ConsumeSubscriptionAsync(instance, subscription, model, cancellationToken);
            await FinalizeTransitionAsync(instance, cancellationToken);
            AddOutbox(instance, "SignalCorrelated", new { signalName });
        }

        CompleteInbox(inbox, subscriptions.Count.ToString(CultureInfo.InvariantCulture));
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask CompleteUserTaskAsync(
        Guid taskId,
        IDictionary<string, object>? variables,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var task = await _db.Tasks.SingleOrDefaultAsync(item => item.Id == taskId, cancellationToken)
                   ?? throw new KeyNotFoundException($"User task '{taskId}' was not found.");
        if (await FindInboxAsync("complete-task", idempotencyKey, task.TenantId, cancellationToken) is not null)
            return;

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        if (task.Status == UserTaskStatus.Completed)
            return;
        if (task.Status is UserTaskStatus.Cancelled or UserTaskStatus.Rejected)
            throw new InvalidOperationException($"Task is in {task.Status} state.");

        var inbox = AddInbox("complete-task", idempotencyKey, task.TenantId);
        if (await ClaimInboxAsync(
                inbox, transaction, "complete-task", idempotencyKey, task.TenantId, cancellationToken) is not null)
            return;
        var instance = await LoadActiveInstanceAsync(task.ProcessInstanceId, task.TenantId, cancellationToken);
        var definition = await _db.ProcessDefinitions.AsNoTracking()
            .SingleAsync(item => item.Id == instance.ProcessDefinitionId, cancellationToken);
        var model = ExecutionModel.Parse(definition.BpmnXml, definition.Key);

        task.Status = UserTaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        task.LastModified = DateTime.UtcNow;
        task.Revision++;
        MergeVariables(instance, variables);
        await CompleteWaitingTaskTokenAsync(instance.Id, task, cancellationToken);
        await CancelBoundaryJobsAsync(instance.Id, task.Id, cancellationToken);
        if (model.Nodes.TryGetValue(task.ActivityId, out var completedNode))
            await RegisterCompensationAsync(instance, completedNode, model, cancellationToken);
        AddHistory(instance, "USER_TASK_COMPLETED", task.ActivityId, new { task.Id });
        AddOutbox(instance, "UserTaskCompleted", new { task.Id, task.ActivityId });
        if (task.LocalVariables.ContainsKey(CompensationBatchVariable))
        {
            var queue = new Queue<PendingNode>();
            await ContinueCompensationAsync(instance, task.LocalVariables, model, queue, cancellationToken);
            await AdvanceAsync(instance, model, queue, cancellationToken);
        }
        else if (task.MultiInstanceExecutionId.HasValue)
        {
            var queue = new Queue<PendingNode>();
            var ownerActivityId = task.LocalVariables.TryGetValue(MultiInstanceActivityIdVariable, out var ownerValue)
                ? ownerValue?.ToString()
                : task.ActivityId;
            if (string.Equals(ownerActivityId, task.ActivityId, StringComparison.Ordinal))
            {
                if (!model.Nodes.TryGetValue(task.ActivityId, out var multiInstanceNode))
                    throw new InvalidOperationException($"Multi-instance activity '{task.ActivityId}' is not defined.");
                await CompleteMultiInstanceIterationAsync(
                    instance,
                    multiInstanceNode,
                    task.MultiInstanceExecutionId.Value,
                    task.MultiInstanceIndex.GetValueOrDefault(),
                    model,
                    queue,
                    cancellationToken);
            }
            else
            {
                var context = new PendingNode(
                    task.ActivityId,
                    null,
                    MultiInstanceExecutionId: task.MultiInstanceExecutionId,
                    MultiInstanceIndex: task.MultiInstanceIndex,
                    LocalVariables: task.LocalVariables,
                    MultiInstanceActivityId: ownerActivityId);
                Enqueue(queue, model.Outgoing(task.ActivityId), context);
            }
            await AdvanceAsync(instance, model, queue, cancellationToken);
        }
        else
        {
            await AdvanceAsync(instance, model, model.Outgoing(task.ActivityId), cancellationToken);
        }
        await FinalizeTransitionAsync(instance, cancellationToken);
        CompleteInbox(inbox, task.Id.ToString());
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    public async ValueTask<bool> ExecuteJobAsync(
        Guid jobId,
        string workerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var job = await _db.Jobs.SingleOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (job is null || job.State != ScheduledJob || job.DueDate > now
            || (job.LockedUntil.HasValue && job.LockedUntil > now && job.LockOwner != workerId))
            return false;

        job.LockOwner = workerId;
        job.LockedUntil = now.AddSeconds(30);
        job.Revision++;
        await _db.SaveChangesAsync(cancellationToken);

        var instance = await LoadActiveInstanceAsync(job.ProcessInstanceId, job.TenantId, cancellationToken);
        var definition = await _db.ProcessDefinitions.AsNoTracking()
            .SingleAsync(item => item.Id == instance.ProcessDefinitionId, cancellationToken);
        var model = ExecutionModel.Parse(definition.BpmnXml, definition.Key);
        var payload = JsonSerializer.Deserialize<TimerPayload>(job.Payload ?? "{}") ?? new TimerPayload();

        if (payload.Kind == "boundary")
        {
            if (payload.Interrupting && payload.TaskId.HasValue)
            {
                var task = await _db.Tasks.SingleOrDefaultAsync(item => item.Id == payload.TaskId.Value, cancellationToken);
                if (task is not null && task.Status is UserTaskStatus.Pending or UserTaskStatus.Delegated)
                {
                    task.Status = UserTaskStatus.Cancelled;
                    task.CompletedAt = now;
                    task.LastModified = now;
                    task.Revision++;
                }
            }
            if (payload.Interrupting && !string.IsNullOrWhiteSpace(payload.AttachedActivityId))
                await CompleteWaitingTokenAsync(instance.Id, payload.AttachedActivityId, cancellationToken);
        }
        else
        {
            await CancelEventGatewayCompetitorsAsync(instance.Id, job.ActivityId, cancellationToken);
            var waitingToken = await _db.ExecutionTokens
                .Where(token => token.ProcessInstanceId == instance.Id
                                && token.CurrentNodeId == job.ActivityId
                                && token.State == ExecutionToken.WaitingState)
                .OrderBy(token => token.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            await CompleteWaitingTokenAsync(instance.Id, job.ActivityId, cancellationToken);
            var resumed = waitingToken is null
                ? model.Outgoing(job.ActivityId)
                : model.Outgoing(job.ActivityId).Select(flow => WithExecutionContext(flow, ExecutionContextFromToken(waitingToken)));
            if (waitingToken is not null)
                await PrepareEventSubprocessTriggerAsync(instance, waitingToken, model, cancellationToken);

            job.State = "Completed";
            job.CompletedAt = now;
            job.LockedUntil = null;
            job.Revision++;
            AddHistory(instance, "TIMER_FIRED", job.ActivityId, new { job.Id, payload.Kind });
            AddOutbox(instance, "TimerFired", new { job.Id, job.ActivityId, payload.Kind });
            await AdvanceAsync(instance, model, resumed, cancellationToken);
            await FinalizeTransitionAsync(instance, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return true;
        }

        job.State = "Completed";
        job.CompletedAt = now;
        job.LockedUntil = null;
        job.Revision++;
        AddHistory(instance, "TIMER_FIRED", job.ActivityId, new { job.Id, payload.Kind });
        AddOutbox(instance, "TimerFired", new { job.Id, job.ActivityId, payload.Kind });
        await AdvanceAsync(instance, model, model.Outgoing(job.ActivityId), cancellationToken);
        await FinalizeTransitionAsync(instance, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async ValueTask RecoverIncidentAsync(
        Guid incidentId,
        string? tenantId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        if (await FindInboxAsync("recover-incident", idempotencyKey, tenantId, cancellationToken) is not null)
            return;

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        var incident = await _db.Incidents.SingleOrDefaultAsync(
            item => item.Id == incidentId && item.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Incident '{incidentId}' was not found.");
        if (incident.State == "Resolved") return;
        if (incident.State != "Open" && incident.State != "DeadLetter")
            throw new InvalidOperationException($"Incident is in '{incident.State}' state.");

        var instance = await _db.ProcessInstances.SingleAsync(
            item => item.Id == incident.ProcessInstanceId && item.TenantId == tenantId, cancellationToken);
        var definition = await _db.ProcessDefinitions.AsNoTracking()
            .SingleAsync(item => item.Id == instance.ProcessDefinitionId, cancellationToken);
        var inbox = AddInbox("recover-incident", idempotencyKey, tenantId);
        if (await ClaimInboxAsync(
                inbox, transaction, "recover-incident", idempotencyKey, tenantId, cancellationToken) is not null)
            return;

        incident.State = "Resolved";
        incident.ResolvedAt = DateTime.UtcNow;
        incident.RetryCount++;
        instance.Status = ProcessInstanceStatus.Running;
        instance.State = "Running";
        instance.EndedAt = null;
        instance.Revision++;

        if (incident.Type == "JobDeadLetter")
        {
            var job = await _db.Jobs
                .Where(item => item.ProcessInstanceId == instance.Id
                               && item.ActivityId == incident.ActivityId
                               && item.State == "DeadLetter")
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The dead-letter job no longer exists.");
            job.State = ScheduledJob;
            job.DueDate = DateTime.UtcNow;
            job.LockOwner = null;
            job.LockedUntil = null;
            job.ErrorMessage = null;
            job.Revision++;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(incident.ActivityId))
                throw new InvalidOperationException("The incident has no recoverable BPMN activity.");
            var model = ExecutionModel.Parse(definition.BpmnXml, definition.Key);
            await AdvanceAsync(instance, model, [new PendingNode(incident.ActivityId, null)], cancellationToken);
        }

        AddHistory(instance, "INCIDENT_RECOVERED", incident.ActivityId ?? "", new { incident.Id, incident.RetryCount });
        AddOutbox(instance, "IncidentRecovered", new { incident.Id, incident.ActivityId, incident.RetryCount });
        await FinalizeTransitionAsync(instance, cancellationToken);
        CompleteInbox(inbox, incident.Id.ToString());
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private async Task ConsumeSubscriptionAsync(
        ProcessInstance instance,
        EventSubscription subscription,
        ExecutionModel model,
        CancellationToken cancellationToken)
    {
        if (subscription.State != ActiveSubscription) return;
        await CancelEventGatewayCompetitorsAsync(instance.Id, subscription.ActivityId, cancellationToken);
        subscription.State = "Consumed";
        subscription.ActiveKey = null;
        subscription.ConsumedAt = DateTime.UtcNow;
        subscription.Revision++;
        var token = await _db.ExecutionTokens.SingleAsync(
            item => item.Id == subscription.ExecutionTokenId,
            cancellationToken);
        token.State = ExecutionToken.CompletedState;
        token.Revision++;
        await PrepareEventSubprocessTriggerAsync(instance, token, model, cancellationToken);
        AddHistory(instance, $"{subscription.EventType.ToUpperInvariant()}_CORRELATED", subscription.ActivityId,
            new { subscription.EventName });
        var context = ExecutionContextFromToken(token);
        await AdvanceAsync(
            instance,
            model,
            model.Outgoing(subscription.ActivityId).Select(flow => WithExecutionContext(flow, context)),
            cancellationToken);
    }

    private async Task AdvanceAsync(
        ProcessInstance instance,
        ExecutionModel model,
        IEnumerable<PendingNode> initial,
        CancellationToken cancellationToken)
    {
        var queue = new Queue<PendingNode>(initial);
        var steps = 0;
        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++steps > MaxAutomaticSteps)
                throw new InvalidOperationException($"Process '{instance.ProcessId}' exceeded {MaxAutomaticSteps} automatic steps.");

            var pending = queue.Dequeue();
            if (!model.Nodes.TryGetValue(pending.NodeId, out var node))
                throw new InvalidOperationException($"Flow node '{pending.NodeId}' is not defined.");

            if (pending.MultiInstanceExecutionId is null
                && node.IsMultiInstance
                && node.Kind is "userTask" or "serviceTask" or "businessRuleTask" or "task" or "manualTask" or "sendTask" or "receiveTask" or "subProcess" or "transaction")
            {
                await StartMultiInstanceAsync(instance, node, model, queue, cancellationToken);
                continue;
            }

            switch (node.Kind)
            {
                case "startEvent":
                    Enqueue(queue, model.Outgoing(node.Id), pending);
                    break;

                case "exclusiveGateway":
                    var exclusiveFlow = SelectExclusiveFlow(node, model.Outgoing(node.Id), instance.Variables);
                    if (exclusiveFlow is null)
                    {
                        await SuspendWithIncidentAsync(
                            instance,
                            node.Id,
                            $"Exclusive gateway '{node.Id}' has no matching condition and no default flow.",
                            cancellationToken);
                        break;
                    }
                    queue.Enqueue(WithExecutionContext(exclusiveFlow, pending));
                    AddHistory(instance, "EXCLUSIVE_GATEWAY_SELECTED", node.Id, new { exclusiveFlow.FlowId });
                    break;

                case "inclusiveGateway":
                    if (model.IncomingCount(node.Id) > 1 && model.OutgoingCount(node.Id) <= 1)
                    {
                        if (await ArriveInclusiveJoinAsync(
                                instance, node, pending, model, queue, cancellationToken))
                            Enqueue(queue, model.Outgoing(node.Id));
                        break;
                    }
                    var inclusiveFlows = SelectInclusiveFlows(node, model.Outgoing(node.Id), instance.Variables);
                    if (inclusiveFlows.Count == 0)
                    {
                        await SuspendWithIncidentAsync(
                            instance,
                            node.Id,
                            $"Inclusive gateway '{node.Id}' has no matching condition and no default flow.",
                            cancellationToken);
                        break;
                    }
                    Enqueue(queue, inclusiveFlows, pending);
                    AddHistory(instance, "INCLUSIVE_GATEWAY_SELECTED", node.Id,
                        new { flowIds = inclusiveFlows.Select(flow => flow.FlowId).ToArray() });
                    break;

                case "parallelGateway":
                    if (model.IncomingCount(node.Id) > 1 && model.OutgoingCount(node.Id) <= 1)
                    {
                        if (await ArriveGatewayJoinAsync(instance, node, pending, model, cancellationToken))
                            Enqueue(queue, model.Outgoing(node.Id), pending);
                    }
                    else
                    {
                        Enqueue(queue, model.Outgoing(node.Id), pending);
                    }
                    break;

                case "eventBasedGateway":
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
                        break;
                    }
                    Enqueue(queue, eventBranches, pending);
                    AddHistory(instance, "EVENT_GATEWAY_ACTIVATED", node.Id,
                        new { branches = eventBranches.Select(branch => branch.NodeId).ToArray() });
                    break;

                case "complexGateway":
                    if (model.IncomingCount(node.Id) > 1 && model.OutgoingCount(node.Id) <= 1)
                    {
                        if (await ArriveGatewayJoinAsync(instance, node, pending, model, cancellationToken))
                            Enqueue(queue, model.Outgoing(node.Id), pending);
                        break;
                    }
                    var complexFlows = SelectInclusiveFlows(node, model.Outgoing(node.Id), instance.Variables);
                    if (complexFlows.Count == 0)
                    {
                        await SuspendWithIncidentAsync(
                            instance,
                            node.Id,
                            $"Complex gateway '{node.Id}' did not activate an outgoing flow.",
                            cancellationToken);
                        break;
                    }
                    Enqueue(queue, complexFlows, pending);
                    AddHistory(instance, "COMPLEX_GATEWAY_ACTIVATED", node.Id,
                        new { flowIds = complexFlows.Select(flow => flow.FlowId).ToArray() });
                    break;

                case "serviceTask":
                    await ExecuteServiceTaskAsync(instance, node, pending.LocalVariables, cancellationToken);
                    if (instance.Status == ProcessInstanceStatus.Running)
                    {
                        await RegisterCompensationAsync(instance, node, model, cancellationToken);
                        await CompleteActivityAsync(instance, node, pending, model, queue, cancellationToken);
                    }
                    break;

                case "userTask":
                    await CreateUserTaskWaitAsync(instance, node, pending, model, cancellationToken);
                    break;

                case "subProcess":
                case "transaction":
                    var subprocessStarts = model.SubprocessStartNodes(node.Id).ToArray();
                    if (subprocessStarts.Length == 0)
                    {
                        // An empty embedded subprocess represents one successful execution.
                        await RegisterCompensationAsync(instance, node, model, cancellationToken);
                        await CompleteActivityAsync(instance, node, pending, model, queue, cancellationToken);
                    }
                    else
                    {
                        Enqueue(queue, subprocessStarts, pending);
                        await ActivateEventSubprocessesAsync(instance, model, node.Id, cancellationToken);
                    }
                    break;

                case "intermediateCatchEvent":
                    _ = await CreateEventWaitAsync(instance, node, pending, cancellationToken);
                    break;

                case "endEvent":
                    var compensationCompleted = pending.LocalVariables?.ContainsKey(CompensationCompletedVariable) == true;
                    if (node.EventType == "Compensation" && !compensationCompleted)
                    {
                        if (await TriggerCompensationAsync(instance, node, model, queue, cancellationToken)) break;
                    }
                    else if (node.EventType == "Terminate")
                        await TerminateScopeAsync(instance, node, model, queue, cancellationToken);
                    else if (node.EventType is "Error" or "Escalation" or "Cancel")
                    {
                        if (node.EventType == "Cancel" && !compensationCompleted
                            && await TriggerCompensationAsync(instance, node, model, queue, cancellationToken))
                            break;
                        if (await HandleScopedThrowAsync(instance, node, model, queue, cancellationToken)
                            == ScopedThrowResult.NotCaught)
                        {
                            await SuspendWithIncidentAsync(
                                instance,
                                node.Id,
                                $"{node.EventType} end event '{node.Id}' has no matching boundary handler.",
                                cancellationToken);
                        }
                        break;
                    }
                    AddHistory(instance, "END_EVENT_REACHED", node.Id, new { node.EventType });
                    if (!string.IsNullOrWhiteSpace(node.ParentSubprocessId)
                        && !await HasOtherActiveScopeWorkAsync(instance.Id, node.ParentSubprocessId, pending, model, queue, cancellationToken))
                    {
                        var completedScope = model.Nodes[node.ParentSubprocessId];
                        if (completedScope.IsEventSubprocess)
                        {
                            await CompleteEventSubprocessAsync(
                                instance, completedScope, model, queue, pending, cancellationToken);
                            break;
                        }

                        await RegisterCompensationAsync(instance, completedScope, model, cancellationToken);

                        if (pending.LocalVariables?.ContainsKey(CompensationBatchVariable) == true)
                        {
                            await ContinueCompensationAsync(
                                instance, pending.LocalVariables, model, queue, cancellationToken);
                            break;
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
                    break;

                case "boundaryEvent":
                    Enqueue(queue, model.Outgoing(node.Id), pending);
                    break;

                case "intermediateThrowEvent":
                    var intermediateCompensationCompleted =
                        pending.LocalVariables?.ContainsKey(CompensationCompletedVariable) == true;
                    if (node.EventType == "Compensation")
                    {
                        if (!intermediateCompensationCompleted
                            && await TriggerCompensationAsync(instance, node, model, queue, cancellationToken))
                            break;
                    }
                    else if (node.EventType is "Error" or "Escalation" or "Cancel")
                    {
                        if (node.EventType == "Cancel" && !intermediateCompensationCompleted
                            && await TriggerCompensationAsync(instance, node, model, queue, cancellationToken))
                            break;
                        var catchResult = await HandleScopedThrowAsync(
                            instance, node, model, queue, cancellationToken);
                        if (catchResult == ScopedThrowResult.NotCaught)
                            await SuspendWithIncidentAsync(
                                instance,
                                node.Id,
                                $"{node.EventType} throw event '{node.Id}' has no matching boundary handler.",
                                cancellationToken);
                        else if (catchResult == ScopedThrowResult.NonInterrupting)
                            Enqueue(queue, model.Outgoing(node.Id), pending);
                        break;
                    }
                    else
                        throw new NotSupportedException($"Throw event '{node.Id}' has unsupported definition '{node.EventType}'.");
                    Enqueue(queue, model.Outgoing(node.Id), pending);
                    break;

                case "task":
                case "manualTask":
                case "sendTask":
                case "receiveTask":
                    await RegisterCompensationAsync(instance, node, model, cancellationToken);
                    await CompleteActivityAsync(instance, node, pending, model, queue, cancellationToken);
                    break;

                case "businessRuleTask":
                    await ExecuteBusinessRuleTaskAsync(instance, node, pending.LocalVariables, cancellationToken);
                    if (instance.Status == ProcessInstanceStatus.Running)
                    {
                        await RegisterCompensationAsync(instance, node, model, cancellationToken);
                        await CompleteActivityAsync(instance, node, pending, model, queue, cancellationToken);
                    }
                    break;

                case "callActivity":
                    await StartCallActivityAsync(instance, node, pending, model, cancellationToken);
                    break;

                case "scriptTask":
                    throw new InvalidOperationException("In-process script task execution is disabled.");

                default:
                    throw new NotSupportedException($"Flow node type '{node.Kind}' is not supported by the production subset.");
            }

            await ReleaseReadyInclusiveJoinsAsync(instance, model, queue, cancellationToken);
        }
    }

    private async Task ExecuteServiceTaskAsync(
        ProcessInstance instance,
        ExecutionNode node,
        IReadOnlyDictionary<string, object>? localVariables,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(node.Implementation)
            || !_serviceTasks.TryResolve(node.Implementation, out var handler)
            || handler is null)
        {
            await SuspendWithIncidentAsync(instance, node.Id,
                $"No service-task handler is registered for '{node.Implementation}'.", cancellationToken);
            return;
        }

        try
        {
            var variables = CreateActivityVariables(instance.Variables, localVariables);
            await handler.ExecuteAsync(node.Attributes, variables, cancellationToken);
            MergeActivityOutputs(instance.Variables, variables, localVariables);
            instance.Variables = new Dictionary<string, object>(instance.Variables, StringComparer.Ordinal);
            AddHistory(instance, "SERVICE_TASK_COMPLETED", node.Id, new { node.Implementation });
            AddOutbox(instance, "ServiceTaskCompleted", new { node.Id, node.Implementation });
        }
        catch (Exception exception)
        {
            await SuspendWithIncidentAsync(instance, node.Id, exception.Message, cancellationToken);
        }
    }

    private async Task StartCallActivityAsync(
        ProcessInstance parent,
        ExecutionNode callActivity,
        PendingNode pending,
        ExecutionModel parentModel,
        CancellationToken cancellationToken)
    {
        if (!callActivity.Attributes.TryGetValue("calledElement", out var calledElement)
            || string.IsNullOrWhiteSpace(calledElement))
        {
            await SuspendWithIncidentAsync(
                parent,
                callActivity.Id,
                $"Call activity '{callActivity.Id}' has no calledElement.",
                cancellationToken);
            return;
        }

        var targetDefinition = await _db.ProcessDefinitions.AsNoTracking()
            .Where(definition => definition.TenantScope == TenantScope(parent.TenantId)
                                 && definition.Key == calledElement)
            .OrderByDescending(definition => definition.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (targetDefinition is null)
        {
            await SuspendWithIncidentAsync(
                parent,
                callActivity.Id,
                $"Called process definition '{calledElement}' was not found for the current tenant.",
                cancellationToken);
            return;
        }

        await EnsureCallDepthAsync(parent, cancellationToken);
        var now = DateTime.UtcNow;
        var child = new ProcessInstance
        {
            Id = Guid.NewGuid(),
            ProcessDefinitionId = targetDefinition.Id,
            ProcessId = targetDefinition.Key,
            BusinessKey = parent.BusinessKey,
            TenantId = parent.TenantId,
            ParentProcessInstanceId = parent.Id,
            CallingActivityId = callActivity.Id,
            StartedAt = now,
            CreatedAt = now,
            LastModified = now,
            State = "Running",
            Status = ProcessInstanceStatus.Running,
            Variables = new Dictionary<string, object>(parent.Variables, StringComparer.Ordinal),
            Revision = 1
        };
        _db.ProcessInstances.Add(child);

        var parentToken = CreateWaitingToken(parent, callActivity);
        StoreExecutionContext(parentToken, pending);
        parentToken.Variables["$vertex.calledProcessInstanceId"] = child.Id.ToString();

        AddHistory(parent, "CALL_ACTIVITY_STARTED", callActivity.Id,
            new { calledElement, childProcessInstanceId = child.Id });
        AddOutbox(parent, "CallActivityStarted",
            new { callActivity.Id, calledElement, childProcessInstanceId = child.Id });
        AddHistory(child, "PROCESS_STARTED", "", new { targetDefinition.Key, targetDefinition.Version, parent.Id });
        AddOutbox(child, "ProcessStarted",
            new { targetDefinition.Key, targetDefinition.Version, parentProcessInstanceId = parent.Id });

        var childModel = ExecutionModel.Parse(targetDefinition.BpmnXml, targetDefinition.Key);
        var starts = childModel.Nodes.Values
            .Where(node => node.Kind == "startEvent" && string.IsNullOrWhiteSpace(node.ParentSubprocessId))
            .Select(node => new PendingNode(node.Id, null))
            .ToArray();
        if (starts.Length == 0)
            throw new InvalidOperationException($"Called process '{calledElement}' has no executable start event.");

        await AdvanceAsync(child, childModel, starts, cancellationToken);
        await ActivateEventSubprocessesAsync(child, childModel, null, cancellationToken);
        await FinalizeTransitionAsync(child, cancellationToken);
    }

    private async Task EnsureCallDepthAsync(ProcessInstance parent, CancellationToken cancellationToken)
    {
        var depth = 1;
        var ancestorId = parent.ParentProcessInstanceId;
        while (ancestorId.HasValue)
        {
            if (++depth > 64)
                throw new InvalidOperationException("Call activity nesting exceeds the maximum depth of 64.");
            ancestorId = await _db.ProcessInstances.AsNoTracking()
                .Where(instance => instance.Id == ancestorId.Value)
                .Select(instance => instance.ParentProcessInstanceId)
                .SingleOrDefaultAsync(cancellationToken);
        }
    }

    private async Task ResumeCallingProcessAsync(ProcessInstance child, CancellationToken cancellationToken)
    {
        if (!child.ParentProcessInstanceId.HasValue || string.IsNullOrWhiteSpace(child.CallingActivityId))
            return;

        var parent = await _db.ProcessInstances
            .SingleOrDefaultAsync(instance => instance.Id == child.ParentProcessInstanceId.Value, cancellationToken);
        if (parent is null || parent.Status != ProcessInstanceStatus.Running) return;

        var token = await _db.ExecutionTokens
            .Where(candidate => candidate.ProcessInstanceId == parent.Id
                                && candidate.CurrentNodeId == child.CallingActivityId
                                && candidate.State == ExecutionToken.WaitingState)
            .OrderBy(candidate => candidate.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (token is null) return;

        token.State = ExecutionToken.CompletedState;
        token.Revision++;
        foreach (var variable in child.Variables)
            parent.Variables[variable.Key] = BpmnConditionEvaluator.NormalizeJsonValue(variable.Value) ?? new object();
        parent.Variables = new Dictionary<string, object>(parent.Variables, StringComparer.Ordinal);

        var definition = await _db.ProcessDefinitions.AsNoTracking()
            .SingleAsync(item => item.Id == parent.ProcessDefinitionId, cancellationToken);
        var model = ExecutionModel.Parse(definition.BpmnXml, definition.Key);
        if (!model.Nodes.TryGetValue(child.CallingActivityId, out var callActivity))
            throw new InvalidOperationException(
                $"Calling activity '{child.CallingActivityId}' is not present in parent definition '{definition.Key}'.");

        AddHistory(parent, "CALL_ACTIVITY_COMPLETED", child.CallingActivityId,
            new { childProcessInstanceId = child.Id, child.ProcessId });
        AddOutbox(parent, "CallActivityCompleted",
            new { activityId = child.CallingActivityId, childProcessInstanceId = child.Id });

        var queue = new Queue<PendingNode>();
        await RegisterCompensationAsync(parent, callActivity, model, cancellationToken);
        await CompleteActivityAsync(
            parent,
            callActivity,
            ExecutionContextFromToken(token),
            model,
            queue,
            cancellationToken);
        await AdvanceAsync(parent, model, queue, cancellationToken);
        await FinalizeTransitionAsync(parent, cancellationToken);
    }

    private async Task ExecuteBusinessRuleTaskAsync(
        ProcessInstance instance,
        ExecutionNode node,
        IReadOnlyDictionary<string, object>? localVariables,
        CancellationToken cancellationToken)
    {
        if (!node.Attributes.TryGetValue("decisionRef", out var decisionKey)
            || string.IsNullOrWhiteSpace(decisionKey))
        {
            await SuspendWithIncidentAsync(
                instance,
                node.Id,
                $"Business rule task '{node.Id}' does not define a decisionRef.",
                cancellationToken);
            return;
        }

        try
        {
            var inputs = CreateActivityVariables(instance.Variables, localVariables);
            var result = await _decisions.EvaluateDecisionByKeyAsync(
                decisionKey,
                inputs,
                instance.TenantId,
                cancellationToken);
            var outputs = new Dictionary<string, object>(result.Variables, StringComparer.Ordinal);

            foreach (var output in outputs)
                instance.Variables[output.Key] = output.Value;

            if (node.Attributes.TryGetValue("resultVariable", out var resultVariable)
                && !string.IsNullOrWhiteSpace(resultVariable))
            {
                instance.Variables[resultVariable] = outputs.Count == 1
                    ? outputs.Values.Single()
                    : outputs;
            }

            instance.Variables = new Dictionary<string, object>(instance.Variables, StringComparer.Ordinal);
            AddHistory(instance, "BUSINESS_RULE_TASK_COMPLETED", node.Id, new
            {
                decisionKey,
                outputs = outputs.Keys.Order(StringComparer.Ordinal).ToArray()
            });
            AddOutbox(instance, "BusinessRuleTaskCompleted", new { node.Id, decisionKey });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Decision {DecisionKey} failed for business rule task {ActivityId}", decisionKey, node.Id);
            await SuspendWithIncidentAsync(instance, node.Id, exception.Message, cancellationToken);
        }
    }

    private async Task SuspendWithIncidentAsync(
        ProcessInstance instance,
        string activityId,
        string message,
        CancellationToken cancellationToken)
    {
        instance.Status = ProcessInstanceStatus.Suspended;
        instance.State = "Incident";
        instance.LastModified = DateTime.UtcNow;
        instance.Revision++;
        _db.Incidents.Add(new Incident
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = instance.Id,
            ActivityId = activityId,
            Type = "ExecutionFailure",
            Message = message,
            CreatedAt = DateTime.UtcNow,
            TenantId = instance.TenantId,
            State = "Open"
        });
        AddOutbox(instance, "IncidentCreated", new { activityId, message });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogError("Process {ProcessInstanceId} suspended at {ActivityId}: {Message}", instance.Id, activityId, message);
    }

    private static Dictionary<string, object> CreateActivityVariables(
        IDictionary<string, object> processVariables,
        IReadOnlyDictionary<string, object>? localVariables)
    {
        var variables = new Dictionary<string, object>(processVariables, StringComparer.Ordinal);
        if (localVariables is not null)
            foreach (var pair in localVariables)
                variables[pair.Key] = pair.Value;
        return variables;
    }

    private static void MergeActivityOutputs(
        IDictionary<string, object> processVariables,
        IReadOnlyDictionary<string, object> activityVariables,
        IReadOnlyDictionary<string, object>? localVariables)
    {
        foreach (var pair in activityVariables)
        {
            if (localVariables?.ContainsKey(pair.Key) == true) continue;
            processVariables[pair.Key] = pair.Value;
        }
    }

    private async Task StartMultiInstanceAsync(
        ProcessInstance instance,
        ExecutionNode node,
        ExecutionModel model,
        Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        var items = ResolveMultiInstanceItems(node, instance.Variables);
        if (items.Count == 0)
        {
            AddHistory(instance, "MULTI_INSTANCE_COMPLETED", node.Id, new { instances = 0 });
            Enqueue(queue, model.Outgoing(node.Id));
            return;
        }

        var execution = new MultiInstanceExecution
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = instance.Id,
            ActivityId = node.Id,
            InstanceCount = items.Count,
            CompletedCount = 0,
            IsSequential = node.Attributes.TryGetValue("multiInstanceSequential", out var sequential)
                           && string.Equals(sequential, "true", StringComparison.OrdinalIgnoreCase),
            NextIndex = 0,
            ItemsJson = JsonSerializer.Serialize(items),
            ElementVariable = node.Attributes.GetValueOrDefault("multiInstanceElementVariable"),
            CompletionCondition = node.Attributes.GetValueOrDefault("multiInstanceCompletionCondition"),
            OutputCollection = node.Attributes.GetValueOrDefault("multiInstanceOutputCollection"),
            State = "Active",
            Revision = 1
        };
        _db.MultiInstanceExecutions.Add(execution);

        var numberToActivate = execution.IsSequential ? 1 : items.Count;
        for (var index = 0; index < numberToActivate; index++)
            queue.Enqueue(CreateMultiInstancePending(node, execution, index, items[index]));
        execution.NextIndex = numberToActivate;
        AddHistory(instance, "MULTI_INSTANCE_STARTED", node.Id,
            new { instances = items.Count, execution.IsSequential });
        await Task.CompletedTask;
    }

    private async Task CompleteActivityAsync(
        ProcessInstance instance,
        ExecutionNode node,
        PendingNode pending,
        ExecutionModel model,
        Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        if (pending.LocalVariables?.ContainsKey(CompensationBatchVariable) == true)
        {
            await ContinueCompensationAsync(
                instance, pending.LocalVariables, model, queue, cancellationToken);
            return;
        }

        if (!pending.MultiInstanceExecutionId.HasValue
            || !string.Equals(pending.MultiInstanceActivityId, node.Id, StringComparison.Ordinal))
        {
            Enqueue(queue, model.Outgoing(node.Id), pending);
            return;
        }

        await CompleteMultiInstanceIterationAsync(
            instance,
            node,
            pending.MultiInstanceExecutionId.Value,
            pending.MultiInstanceIndex.GetValueOrDefault(),
            model,
            queue,
            cancellationToken);
    }

    private async Task CompleteMultiInstanceIterationAsync(
        ProcessInstance instance,
        ExecutionNode node,
        Guid executionId,
        int completedIndex,
        ExecutionModel model,
        Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        var execution = await _db.MultiInstanceExecutions.FindAsync([executionId], cancellationToken)
                        ?? throw new InvalidOperationException($"Multi-instance execution '{executionId}' was not found.");
        if (execution.State != "Active") return;

        execution.CompletedCount++;
        execution.Revision++;
        var completionVariables = new Dictionary<string, object>(instance.Variables, StringComparer.Ordinal)
        {
            ["nrOfInstances"] = execution.InstanceCount,
            ["nrOfCompletedInstances"] = execution.CompletedCount,
            ["nrOfActiveInstances"] = execution.InstanceCount - execution.CompletedCount,
            ["loopCounter"] = completedIndex
        };
        var conditionReached = !string.IsNullOrWhiteSpace(execution.CompletionCondition)
                               && BpmnConditionEvaluator.Evaluate(execution.CompletionCondition, completionVariables);
        var allCompleted = execution.CompletedCount >= execution.InstanceCount;

        if (conditionReached || allCompleted)
        {
            execution.State = "Completed";
            await CancelRemainingMultiInstanceWaitsAsync(instance.Id, execution.Id, cancellationToken);
            AddHistory(instance, "MULTI_INSTANCE_COMPLETED", node.Id,
                new { execution.InstanceCount, execution.CompletedCount, conditionReached });
            Enqueue(queue, model.Outgoing(node.Id));
            return;
        }

        if (execution.IsSequential && execution.NextIndex < execution.InstanceCount)
        {
            var items = JsonSerializer.Deserialize<List<object>>(execution.ItemsJson) ?? [];
            var nextIndex = execution.NextIndex++;
            execution.Revision++;
            queue.Enqueue(CreateMultiInstancePending(node, execution, nextIndex, items[nextIndex]));
        }
    }

    private async Task CancelRemainingMultiInstanceWaitsAsync(
        Guid processInstanceId,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        var executionIdText = executionId.ToString();
        var persistedTokens = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == processInstanceId
                            && token.State == ExecutionToken.WaitingState)
            .ToListAsync(cancellationToken);
        var tokens = _db.ExecutionTokens.Local
            .Where(token => token.ProcessInstanceId == processInstanceId
                            && token.State == ExecutionToken.WaitingState)
            .Concat(persistedTokens)
            .DistinctBy(token => token.Id)
            .ToArray();
        foreach (var token in tokens.Where(token => token.Variables.TryGetValue(MultiInstanceIdVariable, out var value)
                                                    && string.Equals(value?.ToString(), executionIdText, StringComparison.Ordinal)))
        {
            token.State = ExecutionToken.CompletedState;
            token.Revision++;
        }

        var tasks = await _db.Tasks
            .Where(task => task.ProcessInstanceId == processInstanceId
                           && task.MultiInstanceExecutionId == executionId
                           && (task.Status == UserTaskStatus.Pending || task.Status == UserTaskStatus.Delegated))
            .ToListAsync(cancellationToken);
        foreach (var task in tasks)
        {
            task.Status = UserTaskStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;
            task.LastModified = DateTime.UtcNow;
            task.Revision++;
            await CancelBoundaryJobsAsync(processInstanceId, task.Id, cancellationToken);
        }
    }

    private static PendingNode CreateMultiInstancePending(
        ExecutionNode node,
        MultiInstanceExecution execution,
        int index,
        object? item)
    {
        var localVariables = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["loopCounter"] = index,
            ["nrOfInstances"] = execution.InstanceCount,
            ["nrOfCompletedInstances"] = execution.CompletedCount,
            ["nrOfActiveInstances"] = execution.InstanceCount - execution.CompletedCount
        };
        if (!string.IsNullOrWhiteSpace(execution.ElementVariable))
            localVariables[execution.ElementVariable] = BpmnConditionEvaluator.NormalizeJsonValue(item) ?? new object();
        return new PendingNode(node.Id, null, MultiInstanceExecutionId: execution.Id,
            MultiInstanceIndex: index, LocalVariables: localVariables,
            MultiInstanceActivityId: node.Id);
    }

    private static IReadOnlyList<object?> ResolveMultiInstanceItems(
        ExecutionNode node,
        IDictionary<string, object> variables)
    {
       
        if (node.Attributes.TryGetValue("multiInstanceCollection", out var collectionExpression)
            && !string.IsNullOrWhiteSpace(collectionExpression))
        {
            var variableName = NormalizeVariableReference(collectionExpression);
            if (!variables.TryGetValue(variableName, out var collectionValue))
                throw new InvalidOperationException(
                    $"Multi-instance collection '{collectionExpression}' for '{node.Id}' was not found.");
            if (collectionValue is JsonElement { ValueKind: JsonValueKind.Array } jsonArray)
                return jsonArray.EnumerateArray().Select(item => (object?)item.Clone()).ToArray();
            if (collectionValue is System.Collections.IEnumerable enumerable and not string)
                return enumerable.Cast<object?>().ToArray();
            throw new InvalidOperationException(
                $"Multi-instance collection '{collectionExpression}' for '{node.Id}' is not enumerable.");
        }
        if (!node.Attributes.TryGetValue("loopCardinality", out var cardinalityExpression)
            || string.IsNullOrWhiteSpace(cardinalityExpression))
            throw new InvalidOperationException($"Multi-instance activity '{node.Id}' has no collection or loopCardinality.");
        var normalized = NormalizeVariableReference(cardinalityExpression);
        var rawCardinality = int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var literal)
            ? literal
            : variables.TryGetValue(normalized, out var cardinalityValue)
                ? Convert.ToInt32(BpmnConditionEvaluator.NormalizeJsonValue(cardinalityValue), CultureInfo.InvariantCulture)
                : throw new InvalidOperationException(
                    $"Multi-instance loopCardinality '{cardinalityExpression}' for '{node.Id}' cannot be resolved.");
        if (rawCardinality < 0)
            throw new InvalidOperationException($"Multi-instance loopCardinality for '{node.Id}' cannot be negative.");
        return Enumerable.Range(0, rawCardinality).Select(index => (object?)index).ToArray();
    }

    private static string NormalizeVariableReference(string expression)
    {
        var value = expression.Trim();
        if ((value.StartsWith("${", StringComparison.Ordinal) || value.StartsWith("#{", StringComparison.Ordinal))
            && value.EndsWith('}'))
            return value[2..^1].Trim();
        return value.StartsWith('=') ? value[1..].Trim() : value;
    }

    private async Task CreateUserTaskWaitAsync(
        ProcessInstance instance,
        ExecutionNode node,
        PendingNode pending,
        ExecutionModel model,
        CancellationToken cancellationToken)
    {
        var token = CreateWaitingToken(instance, node);
        var task = new UserTask
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = instance.Id,
            ActivityId = node.Id,
            Name = string.IsNullOrWhiteSpace(node.Name) ? node.Id : node.Name,
            Type = "userTask",
            TenantId = instance.TenantId,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            Status = UserTaskStatus.Pending,
            Revision = 1,
            MultiInstanceExecutionId = pending.MultiInstanceExecutionId,
            MultiInstanceIndex = pending.MultiInstanceIndex,
            LocalVariables = pending.LocalVariables is null
                ? []
                : new Dictionary<string, object>(pending.LocalVariables, StringComparer.Ordinal)
        };
        token.Variables[TaskIdVariable] = task.Id.ToString();
        if (pending.MultiInstanceExecutionId.HasValue)
        {
            token.Variables[MultiInstanceIdVariable] = pending.MultiInstanceExecutionId.Value.ToString();
            token.Variables[MultiInstanceIndexVariable] = pending.MultiInstanceIndex.GetValueOrDefault();
            token.Variables[MultiInstanceActivityIdVariable] = pending.MultiInstanceActivityId ?? node.Id;
            task.LocalVariables[MultiInstanceActivityIdVariable] = pending.MultiInstanceActivityId ?? node.Id;
        }
        _db.Tasks.Add(task);
        AddHistory(instance, "USER_TASK_CREATED", node.Id, new { task.Id });
        AddOutbox(instance, "UserTaskCreated", new { taskId = task.Id, activityId = node.Id });

        foreach (var boundary in model.BoundaryEvents(node.Id))
        {
            if (boundary.EventType != "Timer") continue;
            _db.Jobs.Add(new Job
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = instance.Id,
                ActivityId = boundary.Id,
                Type = "timer",
                State = ScheduledJob,
                DueDate = ResolveDueDate(boundary),
                Retries = 0,
                TenantId = instance.TenantId,
                CreatedAt = DateTime.UtcNow,
                Revision = 1,
                Payload = JsonSerializer.Serialize(new TimerPayload
                {
                    Kind = "boundary",
                    TaskId = task.Id,
                    AttachedActivityId = node.Id,
                    Interrupting = !boundary.Attributes.TryGetValue("cancelActivity", out var cancelActivity)
                                   || !string.Equals(cancelActivity, "false", StringComparison.OrdinalIgnoreCase)
                })
            });
        }
        await Task.CompletedTask;
    }

    private Task<ExecutionToken> CreateEventWaitAsync(
        ProcessInstance instance,
        ExecutionNode node,
        PendingNode pending,
        CancellationToken cancellationToken)
    {
        var token = CreateWaitingToken(instance, node);
        if (!string.IsNullOrWhiteSpace(pending.SourceNodeId))
            token.Variables[EventGatewayVariable] = pending.SourceNodeId;
        StoreExecutionContext(token, pending);
        switch (node.EventType)
        {
            case "Timer":
                _db.Jobs.Add(new Job
                {
                    Id = Guid.NewGuid(),
                    ProcessInstanceId = instance.Id,
                    ActivityId = node.Id,
                    Type = "timer",
                    State = ScheduledJob,
                    DueDate = ResolveDueDate(node),
                    Retries = 0,
                    TenantId = instance.TenantId,
                    CreatedAt = DateTime.UtcNow,
                    Revision = 1,
                    Payload = JsonSerializer.Serialize(new TimerPayload { Kind = "catch" })
                });
                break;
            case "Message":
            case "Signal":
                _db.EventSubscriptions.Add(new EventSubscription
                {
                    Id = Guid.NewGuid(),
                    ProcessInstanceId = instance.Id,
                    ExecutionTokenId = token.Id,
                    ActivityId = node.Id,
                    EventType = node.EventType,
                    EventName = node.EventName
                                ?? throw new InvalidOperationException($"{node.EventType} event '{node.Id}' has no name."),
                    TenantId = instance.TenantId,
                    State = ActiveSubscription,
                    ActiveKey = ActiveSubscriptionKey(instance.Id, node.Id),
                    CreatedAt = DateTime.UtcNow,
                    Revision = 1
                });
                break;
            default:
                throw new NotSupportedException($"Catch event '{node.Id}' has unsupported definition '{node.EventType}'.");
        }
        AddHistory(instance, "WAIT_STATE_CREATED", node.Id, new { node.EventType, node.EventName });
        return Task.FromResult(token);
    }

    private async Task ActivateEventSubprocessesAsync(
        ProcessInstance instance,
        ExecutionModel model,
        string? parentScopeId,
        CancellationToken cancellationToken)
    {
        foreach (var eventSubprocess in model.Nodes.Values
                     .Where(node => node.IsEventSubprocess
                                    && string.Equals(node.ParentSubprocessId, parentScopeId, StringComparison.Ordinal)))
        {
            foreach (var start in model.SubprocessStartNodes(eventSubprocess.Id)
                         .Select(pending => model.Nodes[pending.NodeId])
                         .Where(node => node.EventType is "Message" or "Signal" or "Timer"))
            {
                var tracked = _db.ExecutionTokens.Local
                    .Where(token => token.ProcessInstanceId == instance.Id
                                    && token.CurrentNodeId == start.Id)
                    .ToArray();
                var alreadyActive = tracked.Length > 0
                    ? tracked.Any(token => token.State == ExecutionToken.WaitingState)
                    : await _db.ExecutionTokens.AnyAsync(token =>
                            token.ProcessInstanceId == instance.Id
                            && token.CurrentNodeId == start.Id
                            && token.State == ExecutionToken.WaitingState,
                        cancellationToken);
                if (alreadyActive) continue;

                var token = await CreateEventWaitAsync(
                    instance,
                    start,
                    new PendingNode(start.Id, eventSubprocess.Id),
                    cancellationToken);
                token.Variables[EventSubprocessIdVariable] = eventSubprocess.Id;
                token.Variables[EventSubprocessInterruptingVariable] = start.IsInterrupting;
                AddHistory(instance, "EVENT_SUBPROCESS_ARMED", start.Id,
                    new { eventSubprocess.Id, start.EventType, start.EventName, start.IsInterrupting });
            }
        }
    }

    private async Task PrepareEventSubprocessTriggerAsync(
        ProcessInstance instance,
        ExecutionToken token,
        ExecutionModel model,
        CancellationToken cancellationToken)
    {
        if (!token.Variables.TryGetValue(EventSubprocessIdVariable, out var subprocessValue)
            || string.IsNullOrWhiteSpace(subprocessValue?.ToString()))
            return;

        var subprocessId = subprocessValue.ToString()!;
        if (!model.Nodes.TryGetValue(subprocessId, out var eventSubprocess)
            || !eventSubprocess.IsEventSubprocess)
            throw new InvalidOperationException($"Event subprocess '{subprocessId}' is not defined.");

        var interrupting = token.Variables.TryGetValue(EventSubprocessInterruptingVariable, out var interruptingValue)
                           && bool.TryParse(interruptingValue?.ToString(), out var parsed)
                           && parsed;
        if (interrupting)
        {
            var parentScopeId = eventSubprocess.ParentSubprocessId;
            await CancelWaitStatesAsync(
                instance.Id,
                activityId =>
                    !model.IsInScope(activityId, subprocessId)
                    && (parentScopeId is null || model.IsInScope(activityId, parentScopeId)),
                cancellationToken);
        }

        AddHistory(instance, "EVENT_SUBPROCESS_TRIGGERED", token.CurrentNodeId,
            new { subprocessId, interrupting });
    }

    private async Task CompleteEventSubprocessAsync(
        ProcessInstance instance,
        ExecutionNode eventSubprocess,
        ExecutionModel model,
        Queue<PendingNode> queue,
        PendingNode context,
        CancellationToken cancellationToken)
    {
        var start = model.SubprocessStartNodes(eventSubprocess.Id)
            .Select(pending => model.Nodes[pending.NodeId])
            .SingleOrDefault(node => node.EventType is "Message" or "Signal" or "Timer" or "Error" or "Escalation")
            ?? throw new InvalidOperationException(
                $"Event subprocess '{eventSubprocess.Id}' has no supported start event.");
        if (start.IsInterrupting)
        {
            if (!string.IsNullOrWhiteSpace(eventSubprocess.ParentSubprocessId))
                Enqueue(queue, model.Outgoing(eventSubprocess.ParentSubprocessId), context);
        }
        else
        {
            if (start.EventType is "Message" or "Signal" or "Timer")
                await ActivateEventSubprocessesAsync(
                    instance,
                    model,
                    eventSubprocess.ParentSubprocessId,
                    cancellationToken);
        }
    }

    private async Task CancelEventGatewayCompetitorsAsync(
        Guid processInstanceId,
        string selectedActivityId,
        CancellationToken cancellationToken)
    {
        var selectedToken = await _db.ExecutionTokens.FirstOrDefaultAsync(
            token => token.ProcessInstanceId == processInstanceId
                     && token.CurrentNodeId == selectedActivityId
                     && token.State == ExecutionToken.WaitingState,
            cancellationToken);
        if (selectedToken is null
            || !selectedToken.Variables.TryGetValue(EventGatewayVariable, out var gatewayValue)
            || string.IsNullOrWhiteSpace(gatewayValue?.ToString()))
            return;

        var gatewayId = gatewayValue.ToString();
        var waitingTokens = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == processInstanceId
                            && token.State == ExecutionToken.WaitingState)
            .ToListAsync(cancellationToken);
        var competitors = waitingTokens
            .Where(token => token.Id != selectedToken.Id
                            && token.Variables.TryGetValue(EventGatewayVariable, out var value)
                            && string.Equals(value?.ToString(), gatewayId, StringComparison.Ordinal))
            .ToArray();
        if (competitors.Length == 0) return;

        var competitorTokenIds = competitors.Select(token => token.Id).ToHashSet();
        foreach (var token in competitors)
        {
            token.State = ExecutionToken.CompletedState;
            token.Revision++;
        }

        var subscriptions = await _db.EventSubscriptions
            .Where(subscription => competitorTokenIds.Contains(subscription.ExecutionTokenId)
                                   && subscription.State == ActiveSubscription)
            .ToListAsync(cancellationToken);
        foreach (var subscription in subscriptions)
        {
            subscription.State = "Cancelled";
            subscription.ActiveKey = null;
            subscription.ConsumedAt = DateTime.UtcNow;
            subscription.Revision++;
        }

        var competitorActivities = competitors
            .Select(token => token.CurrentNodeId)
            .ToHashSet(StringComparer.Ordinal);
        var jobs = await _db.Jobs
            .Where(job => job.ProcessInstanceId == processInstanceId
                          && job.State == ScheduledJob
                          && competitorActivities.Contains(job.ActivityId))
            .ToListAsync(cancellationToken);
        foreach (var job in jobs)
        {
            job.State = "Cancelled";
            job.CompletedAt = DateTime.UtcNow;
            job.LockOwner = null;
            job.LockedUntil = null;
            job.Revision++;
        }
    }

    private ExecutionToken CreateWaitingToken(ProcessInstance instance, ExecutionNode node)
    {
        var token = new ExecutionToken
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = instance.Id,
            CurrentNodeId = node.Id,
            NodeType = node.Kind,
            Variables = new Dictionary<string, object>(instance.Variables, StringComparer.Ordinal),
            CreatedAt = DateTime.UtcNow,
            State = ExecutionToken.WaitingState,
            Revision = 1
        };
        _db.ExecutionTokens.Add(token);
        return token;
    }

    private async Task<bool> ArriveGatewayJoinAsync(
        ProcessInstance instance,
        ExecutionNode node,
        PendingNode pending,
        ExecutionModel model,
        CancellationToken cancellationToken)
    {
        var arrivalKey = pending.FlowId ?? pending.SourceNodeId ?? string.Empty;
        var arrivals = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == instance.Id
                            && token.CurrentNodeId == node.Id
                            && token.State == ExecutionToken.WaitingState)
            .ToListAsync(cancellationToken);
        foreach (var tracked in _db.ExecutionTokens.Local.Where(token =>
                     token.ProcessInstanceId == instance.Id
                     && token.CurrentNodeId == node.Id
                     && token.State == ExecutionToken.WaitingState))
        {
            if (arrivals.All(token => token.Id != tracked.Id)) arrivals.Add(tracked);
        }
        var arrivedSources = arrivals
            .Select(token => token.Variables.TryGetValue("joinFlowId", out var value) ? value?.ToString() : null)
            .Where(value => !string.IsNullOrEmpty(value))
            .ToHashSet(StringComparer.Ordinal);
        arrivedSources.Add(arrivalKey);

        var activationSatisfied = node.Kind switch
        {
            "parallelGateway" => arrivedSources.Count >= model.IncomingCount(node.Id),
            "complexGateway" when node.Attributes.TryGetValue("activationCondition", out var condition) =>
                BpmnConditionEvaluator.Evaluate(
                    condition,
                    instance.Variables.Append(
                        new KeyValuePair<string, object>("activationCount", arrivedSources.Count))),
            "complexGateway" => arrivedSources.Count >= model.IncomingCount(node.Id),
            _ => throw new InvalidOperationException($"Gateway '{node.Id}' is not a supported join type.")
        };

        if (!activationSatisfied)
        {
            if (!arrivals.Any(token => token.Variables.TryGetValue("joinFlowId", out var value)
                                       && string.Equals(value?.ToString(), arrivalKey, StringComparison.Ordinal)))
            {
                var token = CreateWaitingToken(instance, node);
                token.Variables["joinFlowId"] = arrivalKey;
            }
            return false;
        }

        foreach (var arrival in arrivals)
        {
            arrival.State = ExecutionToken.CompletedState;
            arrival.Revision++;
        }
        AddHistory(
            instance,
            node.Kind == "complexGateway" ? "COMPLEX_JOIN_ACTIVATED" : "PARALLEL_JOIN_COMPLETED",
            node.Id,
            new { incoming = arrivedSources.Count, flowIds = arrivedSources.OrderBy(id => id).ToArray() });
        return true;
    }

    private async Task<bool> ArriveInclusiveJoinAsync(
        ProcessInstance instance,
        ExecutionNode node,
        PendingNode pending,
        ExecutionModel model,
        Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        var arrivalKey = pending.FlowId ?? pending.SourceNodeId ?? string.Empty;
        var arrivals = await LoadJoinArrivalsAsync(instance.Id, node.Id, cancellationToken);
        var arrivedFlowIds = ReadJoinFlowIds(arrivals);
        arrivedFlowIds.Add(arrivalKey);

        if (await HasPotentialInclusiveArrivalAsync(
                instance.Id, node.Id, arrivedFlowIds, model, queue, instance.Variables, cancellationToken))
        {
            if (!arrivals.Any(token => token.Variables.TryGetValue("joinFlowId", out var value)
                                       && string.Equals(value?.ToString(), arrivalKey, StringComparison.Ordinal)))
            {
                var token = CreateWaitingToken(instance, node);
                token.Variables["joinFlowId"] = arrivalKey;
            }
            return false;
        }

        CompleteJoinArrivals(arrivals);
        AddHistory(instance, "INCLUSIVE_JOIN_COMPLETED", node.Id,
            new { flowIds = arrivedFlowIds.OrderBy(id => id).ToArray() });
        return true;
    }

    private async Task ReleaseReadyInclusiveJoinsAsync(
        ProcessInstance instance,
        ExecutionModel model,
        Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        var persisted = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == instance.Id
                            && token.NodeType == "inclusiveGateway"
                            && token.State == ExecutionToken.WaitingState)
            .ToListAsync(cancellationToken);
        foreach (var tracked in _db.ExecutionTokens.Local.Where(token =>
                     token.ProcessInstanceId == instance.Id
                     && token.NodeType == "inclusiveGateway"
                     && token.State == ExecutionToken.WaitingState))
        {
            if (persisted.All(token => token.Id != tracked.Id)) persisted.Add(tracked);
        }

        foreach (var group in persisted.GroupBy(token => token.CurrentNodeId, StringComparer.Ordinal).ToArray())
        {
            var arrivedFlowIds = ReadJoinFlowIds(group);
            if (await HasPotentialInclusiveArrivalAsync(
                    instance.Id,
                    group.Key,
                    arrivedFlowIds,
                    model,
                    queue,
                    instance.Variables,
                    cancellationToken))
                continue;

            CompleteJoinArrivals(group);
            AddHistory(instance, "INCLUSIVE_JOIN_COMPLETED", group.Key,
                new { flowIds = arrivedFlowIds.OrderBy(id => id).ToArray() });
            Enqueue(queue, model.Outgoing(group.Key));
        }
    }

    private async Task<bool> HasPotentialInclusiveArrivalAsync(
        Guid processInstanceId,
        string gatewayId,
        IReadOnlySet<string> arrivedFlowIds,
        ExecutionModel model,
        Queue<PendingNode> queue,
        IDictionary<string, object> variables,
        CancellationToken cancellationToken)
    {
        var missing = model.Incoming(gatewayId)
            .Where(flow => !string.IsNullOrWhiteSpace(flow.FlowId)
                           && !arrivedFlowIds.Contains(flow.FlowId))
            .ToArray();
        if (missing.Length == 0) return false;

        var activeTokens = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == processInstanceId
                            && token.State == ExecutionToken.WaitingState
                            && token.CurrentNodeId != gatewayId)
            .ToListAsync(cancellationToken);
        foreach (var tracked in _db.ExecutionTokens.Local.Where(token =>
                     token.ProcessInstanceId == processInstanceId
                     && token.State == ExecutionToken.WaitingState
                     && token.CurrentNodeId != gatewayId))
        {
            if (activeTokens.All(token => token.Id != tracked.Id)) activeTokens.Add(tracked);
        }

        return missing.Any(incoming =>
            queue.Any(pending => model.CanReachIncoming(pending, incoming, variables))
            || activeTokens.Any(token => model.CanReachIncoming(
                new PendingNode(token.CurrentNodeId, null), incoming, variables)));
    }

    private async Task<bool> HasOtherActiveScopeWorkAsync(
        Guid processInstanceId,
        string scopeId,
        PendingNode completedPath,
        ExecutionModel model,
        Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        bool SameIteration(PendingNode candidate) =>
            !completedPath.MultiInstanceExecutionId.HasValue
            || candidate.MultiInstanceExecutionId == completedPath.MultiInstanceExecutionId
               && candidate.MultiInstanceIndex == completedPath.MultiInstanceIndex;

        if (queue.Any(candidate => model.IsInScope(candidate.NodeId, scopeId) && SameIteration(candidate)))
            return true;

        var tokens = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == processInstanceId
                            && token.State == ExecutionToken.WaitingState)
            .ToListAsync(cancellationToken);
        foreach (var tracked in _db.ExecutionTokens.Local.Where(token =>
                     token.ProcessInstanceId == processInstanceId
                     && token.State == ExecutionToken.WaitingState))
        {
            if (tokens.All(token => token.Id != tracked.Id)) tokens.Add(tracked);
        }

        return tokens.Any(token =>
            token.State == ExecutionToken.WaitingState
            && !token.Variables.ContainsKey(EventSubprocessIdVariable)
            && model.IsInScope(token.CurrentNodeId, scopeId)
            && TokenBelongsToIteration(token, completedPath));
    }

    private static bool TokenBelongsToIteration(ExecutionToken token, PendingNode context)
    {
        if (!context.MultiInstanceExecutionId.HasValue) return true;
        return token.Variables.TryGetValue(MultiInstanceIdVariable, out var executionId)
               && string.Equals(
                   executionId?.ToString(),
                   context.MultiInstanceExecutionId.Value.ToString(),
                   StringComparison.Ordinal)
               && token.Variables.TryGetValue(MultiInstanceIndexVariable, out var index)
               && int.TryParse(index?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndex)
               && parsedIndex == context.MultiInstanceIndex.GetValueOrDefault();
    }

    private async Task<List<ExecutionToken>> LoadJoinArrivalsAsync(
        Guid processInstanceId,
        string gatewayId,
        CancellationToken cancellationToken)
    {
        var arrivals = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == processInstanceId
                            && token.CurrentNodeId == gatewayId
                            && token.State == ExecutionToken.WaitingState)
            .ToListAsync(cancellationToken);
        foreach (var tracked in _db.ExecutionTokens.Local.Where(token =>
                     token.ProcessInstanceId == processInstanceId
                     && token.CurrentNodeId == gatewayId
                     && token.State == ExecutionToken.WaitingState))
        {
            if (arrivals.All(token => token.Id != tracked.Id)) arrivals.Add(tracked);
        }
        return arrivals;
    }

    private static HashSet<string> ReadJoinFlowIds(IEnumerable<ExecutionToken> arrivals) =>
        arrivals.Select(token => token.Variables.TryGetValue("joinFlowId", out var value)
                ? value?.ToString()
                : null)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToHashSet(StringComparer.Ordinal);

    private static void CompleteJoinArrivals(IEnumerable<ExecutionToken> arrivals)
    {
        foreach (var arrival in arrivals)
        {
            arrival.State = ExecutionToken.CompletedState;
            arrival.Revision++;
        }
    }

    private async Task RegisterCompensationAsync(
        ProcessInstance instance,
        ExecutionNode completedNode,
        ExecutionModel model,
        CancellationToken cancellationToken)
    {
        if (completedNode.Attributes.TryGetValue("isForCompensation", out var isForCompensation)
            && string.Equals(isForCompensation, "true", StringComparison.OrdinalIgnoreCase))
            return;
        var handlers = model.BoundaryEvents(completedNode.Id)
            .Where(item => item.EventType == "Compensation")
            .Concat(completedNode.Kind is "subProcess" or "transaction"
                ? model.CompensationEventSubprocessStarts(completedNode.Id)
                : [])
            .ToArray();
        if (handlers.Length == 0) return;

        var persistedCount = await _db.EventSubscriptions.CountAsync(subscription =>
            subscription.ProcessInstanceId == instance.Id
            && subscription.EventType == "Compensation",
            cancellationToken);
        var localCount = _db.EventSubscriptions.Local.Count(subscription =>
            subscription.ProcessInstanceId == instance.Id
            && subscription.EventType == "Compensation"
            && subscription.Id != Guid.Empty);
        var order = persistedCount + localCount;
        foreach (var handler in handlers)
        {
            order++;
            var token = new ExecutionToken
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = instance.Id,
                CurrentNodeId = handler.Id,
                NodeType = handler.Kind == "startEvent" ? "compensationEventSubprocess" : "compensationBoundary",
                Variables = new Dictionary<string, object>(instance.Variables, StringComparer.Ordinal)
                {
                    [CompensationScopeVariable] = completedNode.ParentSubprocessId ?? RootCompensationScope,
                    [CompensationOrderVariable] = order
                },
                CreatedAt = DateTime.UtcNow,
                State = "Compensation",
                Revision = 1
            };
            _db.ExecutionTokens.Add(token);
            _db.EventSubscriptions.Add(new EventSubscription
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = instance.Id,
                ExecutionTokenId = token.Id,
                ActivityId = handler.Id,
                EventType = "Compensation",
                EventName = completedNode.Id,
                TenantId = instance.TenantId,
                State = ActiveSubscription,
                ActiveKey = $"{ActiveSubscriptionKey(instance.Id, handler.Id)}:{token.Id:N}",
                CreatedAt = DateTime.UtcNow,
                Revision = 1
            });
            AddHistory(instance, "COMPENSATION_REGISTERED", completedNode.Id,
                new
                {
                    handlerId = handler.Id,
                    scopeId = completedNode.ParentSubprocessId,
                    order
                });
        }
    }

    private async Task<bool> TriggerCompensationAsync(
        ProcessInstance instance,
        ExecutionNode throwEvent,
        ExecutionModel model,
        Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        // A compensation throw can be reached in the same automatic transition in
        // which the handler subscription was registered. Flush the registration
        // inside the surrounding transaction so the database query observes it and
        // the subscription is durable before the handler is dispatched.
        await _db.SaveChangesAsync(cancellationToken);

        var query = _db.EventSubscriptions.Where(subscription =>
            subscription.ProcessInstanceId == instance.Id
            && subscription.TenantId == instance.TenantId
            && subscription.EventType == "Compensation"
            && subscription.State == ActiveSubscription);
        if (!string.IsNullOrWhiteSpace(throwEvent.EventName))
            query = query.Where(subscription => subscription.EventName == throwEvent.EventName);

        var candidates = await query.ToListAsync(cancellationToken);
        var tokenIds = candidates.Select(subscription => subscription.ExecutionTokenId).ToArray();
        var tokens = await _db.ExecutionTokens
            .Where(token => tokenIds.Contains(token.Id))
            .ToDictionaryAsync(token => token.Id, cancellationToken);
        var scope = throwEvent.ParentSubprocessId ?? RootCompensationScope;
        var subscriptions = candidates
            .Where(subscription => tokens.TryGetValue(subscription.ExecutionTokenId, out var token)
                                   && token.Variables.TryGetValue(CompensationScopeVariable, out var value)
                                   && string.Equals(value?.ToString(), scope, StringComparison.Ordinal))
            .OrderByDescending(subscription => CompensationOrder(tokens[subscription.ExecutionTokenId]))
            .ThenByDescending(subscription => subscription.CreatedAt)
            .ToArray();
        if (subscriptions.Length == 0)
        {
            AddHistory(instance, "COMPENSATION_THROWN", throwEvent.Id,
                new { activityRef = throwEvent.EventName, handlers = 0 });
            return false;
        }

        var batchId = Guid.NewGuid().ToString("N");
        foreach (var subscription in subscriptions)
        {
            subscription.State = "PendingCompensation";
            subscription.ActiveKey = null;
            subscription.Revision++;
            var token = tokens[subscription.ExecutionTokenId];
            token.Variables = new Dictionary<string, object>(token.Variables, StringComparer.Ordinal)
            {
                [CompensationBatchVariable] = batchId
            };
        }
        await ActivateCompensationHandlerAsync(
            subscriptions[0],
            tokens[subscriptions[0].ExecutionTokenId],
            batchId,
            throwEvent.Id,
            model,
            queue);
        AddHistory(instance, "COMPENSATION_THROWN", throwEvent.Id,
            new { activityRef = throwEvent.EventName, handlers = subscriptions.Length });
        AddOutbox(instance, "CompensationTriggered",
            new { throwEvent.Id, activityRef = throwEvent.EventName, handlers = subscriptions.Length });
        return true;
    }

    private static Task ActivateCompensationHandlerAsync(
        EventSubscription subscription,
        ExecutionToken token,
        string batchId,
        string throwEventId,
        ExecutionModel model,
        Queue<PendingNode> queue)
    {
        var targets = model.CompensationHandlerTargets(subscription.ActivityId).ToArray();
        if (targets.Length == 0)
            throw new InvalidOperationException(
                $"Compensation event '{subscription.ActivityId}' has no association to a handler.");

        subscription.State = "Consumed";
        subscription.ConsumedAt = DateTime.UtcNow;
        subscription.Revision++;
        token.State = ExecutionToken.CompletedState;
        token.Revision++;
        var context = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [CompensationBatchVariable] = batchId,
            [CompensationThrowEventVariable] = throwEventId,
            [CompensationSubscriptionVariable] = subscription.Id.ToString()
        };
        Enqueue(queue, targets.Select(target => target with { LocalVariables = context }));
        return Task.CompletedTask;
    }

    private async Task ContinueCompensationAsync(
        ProcessInstance instance,
        IReadOnlyDictionary<string, object> context,
        ExecutionModel model,
        Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        var batchId = context.GetValueOrDefault(CompensationBatchVariable)?.ToString()
                      ?? throw new InvalidOperationException("Compensation handler has no batch context.");
        var throwEventId = context.GetValueOrDefault(CompensationThrowEventVariable)?.ToString()
                           ?? throw new InvalidOperationException("Compensation handler has no continuation event.");
        var pendingSubscriptions = await _db.EventSubscriptions
            .Where(subscription => subscription.ProcessInstanceId == instance.Id
                                   && subscription.EventType == "Compensation"
                                   && subscription.State == "PendingCompensation")
            .ToListAsync(cancellationToken);
        var tokenIds = pendingSubscriptions.Select(subscription => subscription.ExecutionTokenId).ToArray();
        var tokens = await _db.ExecutionTokens
            .Where(token => tokenIds.Contains(token.Id))
            .ToDictionaryAsync(token => token.Id, cancellationToken);
        var next = pendingSubscriptions
            .Where(subscription => tokens.TryGetValue(subscription.ExecutionTokenId, out var token)
                                   && token.Variables.TryGetValue(CompensationBatchVariable, out var value)
                                   && string.Equals(value?.ToString(), batchId, StringComparison.Ordinal))
            .OrderByDescending(subscription => CompensationOrder(tokens[subscription.ExecutionTokenId]))
            .FirstOrDefault();
        if (next is not null)
        {
            await ActivateCompensationHandlerAsync(
                next, tokens[next.ExecutionTokenId], batchId, throwEventId, model, queue);
            return;
        }

        queue.Enqueue(new PendingNode(
            throwEventId,
            null,
            LocalVariables: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [CompensationCompletedVariable] = true
            }));
        AddHistory(instance, "COMPENSATION_COMPLETED", throwEventId, new { batchId });
    }

    private static long CompensationOrder(ExecutionToken token)
    {
        if (!token.Variables.TryGetValue(CompensationOrderVariable, out var value)) return 0;
        return value switch
        {
            long result => result,
            int result => result,
            JsonElement { ValueKind: JsonValueKind.Number } element when element.TryGetInt64(out var result) => result,
            _ when long.TryParse(value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) => result,
            _ => 0
        };
    }

    private async Task<ScopedThrowResult> HandleScopedThrowAsync(
        ProcessInstance instance,
        ExecutionNode throwEvent,
        ExecutionModel model,
        Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        var scopeId = throwEvent.ParentSubprocessId;
        while (true)
        {
            var eventSubprocessStart = model.EventSubprocessStartNodes(scopeId)
                .Where(candidate => EventMatches(candidate, throwEvent))
                .OrderByDescending(candidate => string.Equals(
                    candidate.EventName, throwEvent.EventName, StringComparison.Ordinal))
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (eventSubprocessStart is not null)
            {
                var eventSubprocess = model.Nodes[eventSubprocessStart.ParentSubprocessId!];
                var interrupting = throwEvent.EventType == "Error" || eventSubprocessStart.IsInterrupting;
                if (interrupting)
                {
                    if (string.IsNullOrWhiteSpace(scopeId))
                    {
                        queue.Clear();
                        await CancelWaitStatesAsync(instance.Id, _ => true, cancellationToken);
                    }
                    else
                    {
                        await CancelScopeAsync(instance.Id, scopeId, model, queue, cancellationToken);
                    }
                }

                Enqueue(queue, model.Outgoing(eventSubprocessStart.Id));
                AddHistory(instance, $"{throwEvent.EventType?.ToUpperInvariant()}_CAUGHT", eventSubprocessStart.Id,
                    new { throwEvent.Id, scopeId, eventSubprocessId = eventSubprocess.Id, interrupting });
                return interrupting ? ScopedThrowResult.Interrupting : ScopedThrowResult.NonInterrupting;
            }

            if (!string.IsNullOrWhiteSpace(scopeId))
            {
                var boundary = model.BoundaryEvents(scopeId)
                    .FirstOrDefault(candidate => EventMatches(candidate, throwEvent));
                if (boundary is not null)
                {
                    var interrupting = !boundary.Attributes.TryGetValue("cancelActivity", out var cancelActivity)
                                       || !string.Equals(cancelActivity, "false", StringComparison.OrdinalIgnoreCase);
                    if (interrupting)
                        await CancelScopeAsync(instance.Id, scopeId, model, queue, cancellationToken);
                    Enqueue(queue, model.Outgoing(boundary.Id));
                    AddHistory(instance, $"{throwEvent.EventType?.ToUpperInvariant()}_CAUGHT", boundary.Id,
                        new { throwEvent.Id, scopeId, interrupting });
                    return interrupting ? ScopedThrowResult.Interrupting : ScopedThrowResult.NonInterrupting;
                }
            }

            if (string.IsNullOrWhiteSpace(scopeId)) break;
            scopeId = model.Nodes.TryGetValue(scopeId, out var scope)
                ? scope.ParentSubprocessId
                : null;
        }

        return ScopedThrowResult.NotCaught;
    }

    private static bool EventMatches(ExecutionNode candidate, ExecutionNode throwEvent) =>
        candidate.EventType == throwEvent.EventType
        && (string.IsNullOrWhiteSpace(candidate.EventName)
            || string.IsNullOrWhiteSpace(throwEvent.EventName)
            || string.Equals(candidate.EventName, throwEvent.EventName, StringComparison.Ordinal));

    private async Task TerminateScopeAsync(
        ProcessInstance instance,
        ExecutionNode terminateEvent,
        ExecutionModel model,
        Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(terminateEvent.ParentSubprocessId))
        {
            queue.Clear();
            await CancelWaitStatesAsync(
                instance.Id,
                _ => true,
                cancellationToken);
            AddHistory(instance, "PROCESS_SCOPE_TERMINATED", terminateEvent.Id, null);
            return;
        }

        await CancelScopeAsync(
            instance.Id,
            terminateEvent.ParentSubprocessId,
            model,
            queue,
            cancellationToken);
        AddHistory(instance, "SUBPROCESS_SCOPE_TERMINATED", terminateEvent.Id,
            new { scopeId = terminateEvent.ParentSubprocessId });
    }

    private async Task CancelScopeAsync(
        Guid processInstanceId,
        string scopeId,
        ExecutionModel model,
        Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        var retained = queue.Where(pending => !model.IsInScope(pending.NodeId, scopeId)).ToArray();
        queue.Clear();
        Enqueue(queue, retained);
        await CancelWaitStatesAsync(
            processInstanceId,
            activityId => model.IsInScope(activityId, scopeId),
            cancellationToken);
    }

    private async Task CancelWaitStatesAsync(
        Guid processInstanceId,
        Func<string, bool> belongsToScope,
        CancellationToken cancellationToken)
    {
        var persistedTokens = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == processInstanceId
                            && token.State == ExecutionToken.WaitingState)
            .ToListAsync(cancellationToken);
        var tokens = _db.ExecutionTokens.Local
            .Where(token => token.ProcessInstanceId == processInstanceId
                            && token.State == ExecutionToken.WaitingState)
            .Concat(persistedTokens)
            .DistinctBy(token => token.Id)
            .ToArray();
        foreach (var token in tokens.Where(token => belongsToScope(token.CurrentNodeId)))
        {
            token.State = ExecutionToken.CompletedState;
            token.Revision++;
        }

        var persistedTasks = await _db.Tasks
            .Where(task => task.ProcessInstanceId == processInstanceId
                           && (task.Status == UserTaskStatus.Pending || task.Status == UserTaskStatus.Delegated))
            .ToListAsync(cancellationToken);
        var tasks = _db.Tasks.Local
            .Where(task => task.ProcessInstanceId == processInstanceId
                           && (task.Status == UserTaskStatus.Pending || task.Status == UserTaskStatus.Delegated))
            .Concat(persistedTasks)
            .DistinctBy(task => task.Id)
            .ToArray();
        foreach (var task in tasks.Where(task => belongsToScope(task.ActivityId)))
        {
            task.Status = UserTaskStatus.Cancelled;
            task.CompletedAt = DateTime.UtcNow;
            task.LastModified = DateTime.UtcNow;
            task.Revision++;
        }

        var persistedJobs = await _db.Jobs
            .Where(job => job.ProcessInstanceId == processInstanceId && job.State == ScheduledJob)
            .ToListAsync(cancellationToken);
        var jobs = _db.Jobs.Local
            .Where(job => job.ProcessInstanceId == processInstanceId && job.State == ScheduledJob)
            .Concat(persistedJobs)
            .DistinctBy(job => job.Id)
            .ToArray();
        foreach (var job in jobs.Where(job => belongsToScope(job.ActivityId)))
        {
            job.State = "Cancelled";
            job.CompletedAt = DateTime.UtcNow;
            job.LockOwner = null;
            job.LockedUntil = null;
            job.Revision++;
        }

        var persistedSubscriptions = await _db.EventSubscriptions
            .Where(subscription => subscription.ProcessInstanceId == processInstanceId
                                   && subscription.State == ActiveSubscription)
            .ToListAsync(cancellationToken);
        var subscriptions = _db.EventSubscriptions.Local
            .Where(subscription => subscription.ProcessInstanceId == processInstanceId
                                   && subscription.State == ActiveSubscription)
            .Concat(persistedSubscriptions)
            .DistinctBy(subscription => subscription.Id)
            .ToArray();
        foreach (var subscription in subscriptions.Where(subscription => belongsToScope(subscription.ActivityId)))
        {
            subscription.State = "Cancelled";
            subscription.ActiveKey = null;
            subscription.ConsumedAt = DateTime.UtcNow;
            subscription.Revision++;
        }
    }

    private async Task CompleteWaitingTokenAsync(
        Guid processInstanceId,
        string activityId,
        CancellationToken cancellationToken)
    {
        var tokens = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == processInstanceId
                            && token.CurrentNodeId == activityId
                            && token.State == ExecutionToken.WaitingState)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens)
        {
            token.State = ExecutionToken.CompletedState;
            token.Revision++;
        }
    }

    private async Task CompleteWaitingTaskTokenAsync(
        Guid processInstanceId,
        UserTask task,
        CancellationToken cancellationToken)
    {
        var taskId = task.Id.ToString();
        var tokens = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == processInstanceId
                            && token.CurrentNodeId == task.ActivityId
                            && token.State == ExecutionToken.WaitingState)
            .ToListAsync(cancellationToken);
        var token = tokens.FirstOrDefault(candidate =>
            candidate.Variables.TryGetValue(TaskIdVariable, out var value)
            && string.Equals(value?.ToString(), taskId, StringComparison.Ordinal));
        token ??= task.MultiInstanceExecutionId.HasValue ? null : tokens.SingleOrDefault();
        if (token is null)
            throw new InvalidOperationException($"Waiting token for user task '{task.Id}' was not found.");
        token.State = ExecutionToken.CompletedState;
        token.Revision++;
    }

    private async Task CancelBoundaryJobsAsync(
        Guid processInstanceId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var jobs = await _db.Jobs
            .Where(job => job.ProcessInstanceId == processInstanceId
                          && job.State == ScheduledJob
                          && job.Payload != null)
            .ToListAsync(cancellationToken);
        foreach (var job in jobs)
        {
            var payload = JsonSerializer.Deserialize<TimerPayload>(job.Payload!);
            if (payload?.TaskId != taskId) continue;
            job.State = "Cancelled";
            job.CompletedAt = DateTime.UtcNow;
            job.Revision++;
        }
    }

    private async Task FinalizeTransitionAsync(ProcessInstance instance, CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken);
        var waitingTokens = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == instance.Id && token.State == ExecutionToken.WaitingState)
            .ToListAsync(cancellationToken);
        var eventSubprocessTokenIds = waitingTokens
            .Where(token => token.Variables.ContainsKey(EventSubprocessIdVariable))
            .Select(token => token.Id)
            .ToHashSet();
        var eventSubprocessActivities = waitingTokens
            .Where(token => eventSubprocessTokenIds.Contains(token.Id))
            .Select(token => token.CurrentNodeId)
            .ToHashSet(StringComparer.Ordinal);
        var activeTokens = waitingTokens
            .Where(token => !eventSubprocessTokenIds.Contains(token.Id))
            .Select(token => token.CurrentNodeId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        var activeTasks = await _db.Tasks.AsNoTracking()
            .Where(task => task.ProcessInstanceId == instance.Id
                           && (task.Status == UserTaskStatus.Pending || task.Status == UserTaskStatus.Delegated))
            .Select(task => task.ActivityId)
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
        var activeJobs = await _db.Jobs.AsNoTracking()
            .AnyAsync(job => job.ProcessInstanceId == instance.Id
                             && job.State == ScheduledJob
                             && !eventSubprocessActivities.Contains(job.ActivityId), cancellationToken);
        var activeSubscriptions = await _db.EventSubscriptions.AsNoTracking()
            .AnyAsync(subscription => subscription.ProcessInstanceId == instance.Id
                                      && subscription.State == ActiveSubscription
                                      && subscription.EventType != "Compensation"
                                      && !eventSubprocessTokenIds.Contains(subscription.ExecutionTokenId), cancellationToken);
        var activeMultiInstanceExecution = await _db.MultiInstanceExecutions.AsNoTracking()
            .AnyAsync(execution => execution.ProcessInstanceId == instance.Id
                                   && execution.State == "Active", cancellationToken);

        instance.ActiveTokens = activeTokens;
        instance.ActiveTasks = activeTasks;
        instance.LastModified = DateTime.UtcNow;
        instance.Revision++;
        if (instance.Status == ProcessInstanceStatus.Running
            && activeTokens.Count == 0
            && activeTasks.Count == 0
            && !activeJobs
            && !activeSubscriptions
            && !activeMultiInstanceExecution)
        {
            foreach (var triggerToken in waitingTokens.Where(token => eventSubprocessTokenIds.Contains(token.Id)))
            {
                triggerToken.State = ExecutionToken.CompletedState;
                triggerToken.Revision++;
            }
            var triggerSubscriptions = await _db.EventSubscriptions
                .Where(subscription => eventSubprocessTokenIds.Contains(subscription.ExecutionTokenId)
                                       && subscription.State == ActiveSubscription)
                .ToListAsync(cancellationToken);
            foreach (var subscription in triggerSubscriptions)
            {
                subscription.State = "Cancelled";
                subscription.ActiveKey = null;
                subscription.ConsumedAt = DateTime.UtcNow;
                subscription.Revision++;
            }
            var triggerJobs = await _db.Jobs
                .Where(job => job.ProcessInstanceId == instance.Id
                              && job.State == ScheduledJob
                              && eventSubprocessActivities.Contains(job.ActivityId))
                .ToListAsync(cancellationToken);
            foreach (var job in triggerJobs)
            {
                job.State = "Cancelled";
                job.CompletedAt = DateTime.UtcNow;
                job.Revision++;
            }
            instance.Status = ProcessInstanceStatus.Completed;
            instance.State = "Completed";
            instance.EndedAt = DateTime.UtcNow;
            var compensationSubscriptions = await _db.EventSubscriptions
                .Where(subscription => subscription.ProcessInstanceId == instance.Id
                                       && subscription.EventType == "Compensation"
                                       && subscription.State == ActiveSubscription)
                .ToListAsync(cancellationToken);
            foreach (var subscription in compensationSubscriptions)
            {
                subscription.State = "Cancelled";
                subscription.ActiveKey = null;
                subscription.ConsumedAt = DateTime.UtcNow;
                subscription.Revision++;
                var token = await _db.ExecutionTokens.SingleAsync(
                    item => item.Id == subscription.ExecutionTokenId, cancellationToken);
                token.State = ExecutionToken.CompletedState;
                token.Revision++;
            }
            AddHistory(instance, "PROCESS_COMPLETED", "", null);
            AddOutbox(instance, "ProcessCompleted", null);
        }
        else if (instance.Status == ProcessInstanceStatus.Running)
        {
            instance.State = "Waiting";
        }
        await SynchronizeVariablesAsync(instance, cancellationToken);
        if (instance.Status == ProcessInstanceStatus.Completed && instance.ParentProcessInstanceId.HasValue)
            await ResumeCallingProcessAsync(instance, cancellationToken);
    }

    private async Task SynchronizeVariablesAsync(ProcessInstance instance, CancellationToken cancellationToken)
    {
        var existing = await _db.Variables
            .Where(variable => variable.ProcessInstanceId == instance.Id && variable.ScopeId == instance.Id)
            .ToDictionaryAsync(variable => variable.Name, StringComparer.Ordinal, cancellationToken);
        foreach (var pair in instance.Variables)
        {
            var serialized = JsonSerializer.Serialize(pair.Value);
            var type = pair.Value?.GetType().Name ?? "null";
            if (existing.TryGetValue(pair.Key, out var variable))
            {
                variable.Value = serialized;
                variable.Type = type;
            }
            else
            {
                _db.Variables.Add(new Variable
                {
                    Id = Guid.NewGuid(),
                    ProcessInstanceId = instance.Id,
                    ScopeId = instance.Id,
                    Name = pair.Key,
                    Type = type,
                    Value = serialized,
                    TenantId = instance.TenantId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        instance.Variables = new Dictionary<string, object>(instance.Variables, StringComparer.Ordinal);
    }

    private async Task<ProcessInstance> LoadActiveInstanceAsync(
        Guid instanceId,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        var instance = await _db.ProcessInstances.SingleOrDefaultAsync(
            item => item.Id == instanceId && item.TenantId == tenantId,
            cancellationToken) ?? throw new KeyNotFoundException($"Process instance '{instanceId}' was not found.");
        if (instance.Status != ProcessInstanceStatus.Running)
            throw new InvalidOperationException($"Process instance '{instanceId}' is in {instance.Status} state.");
        return instance;
    }

    private static void MergeVariables(ProcessInstance instance, IDictionary<string, object>? variables)
    {
        if (variables is null) return;
        var merged = new Dictionary<string, object>(instance.Variables, StringComparer.Ordinal);
        foreach (var pair in variables) merged[pair.Key] = pair.Value;
        instance.Variables = merged;
    }

    private void AddHistory(ProcessInstance instance, string eventType, string elementId, object? data)
    {
        _db.HistoryEvents.Add(new HistoryEvent
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = instance.Id,
            EventType = eventType,
            ElementId = elementId,
            Timestamp = DateTime.UtcNow,
            TenantId = instance.TenantId,
            Data = data is null ? null : JsonSerializer.Serialize(data)
        });
    }

    private void AddOutbox(ProcessInstance instance, string eventType, object? payload)
    {
        _db.RuntimeOutbox.Add(new RuntimeOutboxMessage
        {
            Id = Guid.NewGuid(),
            ProcessInstanceId = instance.Id,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(payload ?? new { }),
            TenantId = instance.TenantId,
            OccurredAt = DateTime.UtcNow,
            State = "Pending"
        });
    }

    private RuntimeInboxMessage? AddInbox(string operation, string? key, string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var inbox = new RuntimeInboxMessage
        {
            Id = Guid.NewGuid(),
            Operation = operation,
            IdempotencyKey = key.Trim(),
            TenantId = tenantId,
            TenantScope = TenantScope(tenantId),
            ReceivedAt = DateTime.UtcNow
        };
        _db.RuntimeInbox.Add(inbox);
        return inbox;
    }

    private async Task<RuntimeInboxMessage?> FindInboxAsync(
        string operation,
        string? key,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return await _db.RuntimeInbox.AsNoTracking().SingleOrDefaultAsync(
            item => item.Operation == operation
                    && item.IdempotencyKey == key.Trim()
                    && item.TenantScope == TenantScope(tenantId)
                    && item.CompletedAt != null,
            cancellationToken);
    }

    private async Task<RuntimeInboxMessage?> ClaimInboxAsync(
        RuntimeInboxMessage? inbox,
        IDbContextTransaction? transaction,
        string operation,
        string? key,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        if (inbox is null) return null;
        try
        {
            // Persist the claim before executing handlers or changing runtime
            // state. The unique tenant/operation/key constraint serializes
            // concurrent replicas and prevents duplicate external side effects.
            await _db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException exception)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            var duplicate = await FindInboxAsync(operation, key, tenantId, cancellationToken);
            if (duplicate is not null) return duplicate;
            throw new InvalidOperationException(
                $"Failed to acquire idempotency key '{key}' for operation '{operation}'.", exception);
        }
    }

    private static void CompleteInbox(RuntimeInboxMessage? inbox, string result)
    {
        if (inbox is null) return;
        inbox.Result = result;
        inbox.CompletedAt = DateTime.UtcNow;
    }

    private async ValueTask<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken)
        => _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

    private static void EnsureTenant(string? expected, string? actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("The process definition does not belong to the requested tenant.");
    }

    private static string TenantScope(string? tenantId) =>
        string.IsNullOrWhiteSpace(tenantId) ? "$global" : tenantId.Trim();

    private static string ActiveSubscriptionKey(Guid processInstanceId, string activityId) =>
        $"{processInstanceId:N}:{activityId}";

    private static DateTime ResolveDueDate(ExecutionNode node)
    {
        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(node.TimeDate)
            && DateTimeOffset.TryParse(node.TimeDate, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var date))
            return date.UtcDateTime;
        var duration = node.TimeDuration;
        if (string.IsNullOrWhiteSpace(duration) && !string.IsNullOrWhiteSpace(node.TimeCycle))
            duration = node.TimeCycle.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault(part => part.StartsWith('P'));
        if (string.IsNullOrWhiteSpace(duration))
            throw new InvalidOperationException($"Timer event '{node.Id}' has no supported due date.");
        try
        {
            return now.Add(XmlConvert.ToTimeSpan(duration));
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"Timer event '{node.Id}' has invalid ISO-8601 duration '{duration}'.", exception);
        }
    }

    private static void Enqueue(
        Queue<PendingNode> queue,
        IEnumerable<PendingNode> nodes,
        PendingNode? executionContext = null)
    {
        foreach (var node in nodes)
            queue.Enqueue(executionContext is null ? node : WithExecutionContext(node, executionContext));
    }

    private static PendingNode WithExecutionContext(PendingNode node, PendingNode context) =>
        node with
        {
            MultiInstanceExecutionId = context.MultiInstanceExecutionId,
            MultiInstanceIndex = context.MultiInstanceIndex,
            LocalVariables = context.LocalVariables ?? node.LocalVariables,
            MultiInstanceActivityId = context.MultiInstanceActivityId
        };

    private static PendingNode ExecutionContextFromToken(ExecutionToken token)
    {
        var locals = token.Variables
            .Where(pair => pair.Key is not TaskIdVariable and not EventGatewayVariable)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (!token.Variables.TryGetValue(MultiInstanceIdVariable, out var executionValue)
            || !Guid.TryParse(executionValue?.ToString(), out var executionId))
            return new PendingNode(token.CurrentNodeId, null, LocalVariables: locals);

        var index = token.Variables.TryGetValue(MultiInstanceIndexVariable, out var indexValue)
                    && int.TryParse(indexValue?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
        var owner = token.Variables.TryGetValue(MultiInstanceActivityIdVariable, out var ownerValue)
            ? ownerValue?.ToString()
            : null;
        return new PendingNode(
            token.CurrentNodeId,
            null,
            MultiInstanceExecutionId: executionId,
            MultiInstanceIndex: index,
            LocalVariables: locals,
            MultiInstanceActivityId: owner);
    }

    private static void StoreExecutionContext(ExecutionToken token, PendingNode context)
    {
        if (context.MultiInstanceExecutionId.HasValue)
        {
            token.Variables[MultiInstanceIdVariable] = context.MultiInstanceExecutionId.Value.ToString();
            token.Variables[MultiInstanceIndexVariable] = context.MultiInstanceIndex.GetValueOrDefault();
            token.Variables[MultiInstanceActivityIdVariable] = context.MultiInstanceActivityId ?? string.Empty;
        }
        if (context.LocalVariables is not null)
            foreach (var pair in context.LocalVariables)
                token.Variables[pair.Key] = pair.Value;
    }

    private static PendingNode? SelectExclusiveFlow(
        ExecutionNode gateway,
        IEnumerable<PendingNode> outgoing,
        IDictionary<string, object> variables)
    {
        var flows = outgoing.ToArray();
        var defaults = flows.Where(flow => flow.IsDefault).ToArray();
        if (defaults.Length > 1)
            throw new InvalidOperationException($"Gateway '{gateway.Id}' declares more than one default flow.");

        foreach (var flow in flows.Where(flow => !flow.IsDefault))
        {
            if (string.IsNullOrWhiteSpace(flow.ConditionExpression)
                || BpmnConditionEvaluator.Evaluate(
                    flow.ConditionExpression,
                    variables,
                    flow.ConditionExpressionLanguage))
                return flow;
        }

        return defaults.SingleOrDefault();
    }

    private static IReadOnlyList<PendingNode> SelectInclusiveFlows(
        ExecutionNode gateway,
        IEnumerable<PendingNode> outgoing,
        IDictionary<string, object> variables)
    {
        var flows = outgoing.ToArray();
        var defaults = flows.Where(flow => flow.IsDefault).ToArray();
        if (defaults.Length > 1)
            throw new InvalidOperationException($"Gateway '{gateway.Id}' declares more than one default flow.");

        var selected = flows
            .Where(flow => !flow.IsDefault)
            .Where(flow => string.IsNullOrWhiteSpace(flow.ConditionExpression)
                           || BpmnConditionEvaluator.Evaluate(
                               flow.ConditionExpression,
                               variables,
                               flow.ConditionExpressionLanguage))
            .ToArray();
        return selected.Length > 0 ? selected : defaults;
    }

    private sealed class TimerPayload
    {
        public string Kind { get; set; } = "catch";
        public Guid? TaskId { get; set; }
        public string? AttachedActivityId { get; set; }
        public bool Interrupting { get; set; } = true;
    }

    private sealed record PendingNode(
        string NodeId,
        string? SourceNodeId,
        string? FlowId = null,
        string? ConditionExpression = null,
        bool IsDefault = false,
        string? ConditionExpressionLanguage = null,
        Guid? MultiInstanceExecutionId = null,
        int? MultiInstanceIndex = null,
        IReadOnlyDictionary<string, object>? LocalVariables = null,
        string? MultiInstanceActivityId = null);

    private enum ScopedThrowResult
    {
        NotCaught,
        Interrupting,
        NonInterrupting
    }

    private sealed record ExecutionNode(
        string Id,
        string Kind,
        string Name,
        string? Implementation,
        string? AttachedToRef,
        string? ParentSubprocessId,
        string? EventType,
        string? EventName,
        string? TimeDate,
        string? TimeDuration,
        string? TimeCycle,
        Dictionary<string, string> Attributes)
    {
        public bool IsMultiInstance => Attributes.ContainsKey("multiInstanceSequential");
        public bool IsEventSubprocess => Kind == "subProcess"
                                         && Attributes.TryGetValue("triggeredByEvent", out var value)
                                         && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        public bool IsInterrupting => !Attributes.TryGetValue("isInterrupting", out var value)
                                      || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ExecutionModel
    {
        private readonly Dictionary<string, List<PendingNode>> _outgoing;
        private readonly Dictionary<string, List<PendingNode>> _incoming;
        private readonly Dictionary<string, List<ExecutionNode>> _boundaries;
        private readonly Dictionary<string, List<string>> _associationTargets;

        private ExecutionModel(
            Dictionary<string, ExecutionNode> nodes,
            Dictionary<string, List<PendingNode>> outgoing,
            Dictionary<string, List<PendingNode>> incoming,
            Dictionary<string, List<ExecutionNode>> boundaries,
            Dictionary<string, List<string>> associationTargets)
        {
            Nodes = nodes;
            _outgoing = outgoing;
            _incoming = incoming;
            _boundaries = boundaries;
            _associationTargets = associationTargets;
        }

        public IReadOnlyDictionary<string, ExecutionNode> Nodes { get; }
        public IEnumerable<PendingNode> Outgoing(string nodeId)
            => _outgoing.TryGetValue(nodeId, out var nodes) ? nodes : [];
        public int IncomingCount(string nodeId) => _incoming.GetValueOrDefault(nodeId)?.Count ?? 0;
        public IEnumerable<PendingNode> Incoming(string nodeId)
            => _incoming.TryGetValue(nodeId, out var nodes) ? nodes : [];
        public int OutgoingCount(string nodeId) => _outgoing.GetValueOrDefault(nodeId)?.Count ?? 0;
        public IEnumerable<ExecutionNode> BoundaryEvents(string activityId)
            => _boundaries.TryGetValue(activityId, out var nodes) ? nodes : [];
        public IEnumerable<PendingNode> CompensationHandlerTargets(string compensationEventId)
            => _associationTargets.TryGetValue(compensationEventId, out var targets)
                ? targets.Select(target => new PendingNode(target, compensationEventId))
                : Outgoing(compensationEventId);
        public IEnumerable<ExecutionNode> CompensationEventSubprocessStarts(string completedSubprocessId)
            => EventSubprocessStartNodes(completedSubprocessId)
                .Where(start => start.EventType == "Compensation");
        public bool IsInScope(string activityId, string scopeId)
        {
            if (string.Equals(activityId, scopeId, StringComparison.Ordinal)) return true;
            if (!Nodes.TryGetValue(activityId, out var node)) return false;
            var parentId = node.ParentSubprocessId;
            while (!string.IsNullOrWhiteSpace(parentId))
            {
                if (string.Equals(parentId, scopeId, StringComparison.Ordinal)) return true;
                parentId = Nodes.TryGetValue(parentId, out var parent)
                    ? parent.ParentSubprocessId
                    : null;
            }
            return false;
        }
        public bool CanReachIncoming(
            PendingNode active,
            PendingNode incoming,
            IDictionary<string, object> variables)
        {
            if (!string.IsNullOrWhiteSpace(active.FlowId)
                && string.Equals(active.FlowId, incoming.FlowId, StringComparison.Ordinal))
                return true;
            if (string.IsNullOrWhiteSpace(incoming.SourceNodeId)) return false;

            var pending = new Stack<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            pending.Push(active.NodeId);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!visited.Add(current)) continue;
                if (string.Equals(current, incoming.SourceNodeId, StringComparison.Ordinal)) return true;
                if (!Nodes.TryGetValue(current, out var node)) continue;

                IEnumerable<PendingNode> candidates = node.Kind switch
                {
                    "exclusiveGateway" => SelectExclusiveFlow(node, Outgoing(current), variables) is { } selected
                        ? [selected]
                        : [],
                    "inclusiveGateway" or "complexGateway" when IncomingCount(current) <= 1
                        || OutgoingCount(current) > 1 => SelectInclusiveFlows(node, Outgoing(current), variables),
                    _ => Outgoing(current)
                };
                foreach (var candidate in candidates)
                {
                    if (string.Equals(candidate.NodeId, incoming.NodeId, StringComparison.Ordinal)
                        && !string.Equals(candidate.FlowId, incoming.FlowId, StringComparison.Ordinal))
                        continue;
                    pending.Push(candidate.NodeId);
                }
            }
            return false;
        }
        public IEnumerable<PendingNode> SubprocessStartNodes(string subprocessId)
            => Nodes.Values
                .Where(node => node.Kind == "startEvent"
                               && string.Equals(node.ParentSubprocessId, subprocessId, StringComparison.Ordinal))
                .Select(node => new PendingNode(node.Id, subprocessId));

        public IEnumerable<ExecutionNode> EventSubprocessStartNodes(string? parentScopeId)
            => Nodes.Values
                .Where(node => node.IsEventSubprocess
                               && string.Equals(node.ParentSubprocessId, parentScopeId, StringComparison.Ordinal))
                .SelectMany(eventSubprocess => Nodes.Values.Where(node =>
                    node.Kind == "startEvent"
                    && string.Equals(node.ParentSubprocessId, eventSubprocess.Id, StringComparison.Ordinal)));

        public static ExecutionModel Parse(string xml, string processKey)
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var process = document.Descendants().FirstOrDefault(element =>
                element.Name.LocalName == "process"
                && string.Equals((string?)element.Attribute("id"), processKey, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"BPMN process '{processKey}' was not found in the deployed XML.");
            var messages = document.Descendants().Where(element => element.Name.LocalName == "message")
                .Where(element => element.Attribute("id") is not null)
                .ToDictionary(element => (string)element.Attribute("id")!, element =>
                    (string?)element.Attribute("name") ?? (string)element.Attribute("id")!, StringComparer.Ordinal);
            var signals = document.Descendants().Where(element => element.Name.LocalName == "signal")
                .Where(element => element.Attribute("id") is not null)
                .ToDictionary(element => (string)element.Attribute("id")!, element =>
                    (string?)element.Attribute("name") ?? (string)element.Attribute("id")!, StringComparer.Ordinal);

            var nodes = new Dictionary<string, ExecutionNode>(StringComparer.Ordinal);
            foreach (var element in process.DescendantsAndSelf().Where(IsFlowNode))
            {
                var id = (string?)element.Attribute("id");
                if (string.IsNullOrWhiteSpace(id)) continue;
                var definition = element.Elements().FirstOrDefault(child => child.Name.LocalName.EndsWith("EventDefinition", StringComparison.Ordinal));
                var eventType = definition?.Name.LocalName switch
                {
                    "timerEventDefinition" => "Timer",
                    "messageEventDefinition" => "Message",
                    "signalEventDefinition" => "Signal",
                    "compensateEventDefinition" => "Compensation",
                    "errorEventDefinition" => "Error",
                    "escalationEventDefinition" => "Escalation",
                    "cancelEventDefinition" => "Cancel",
                    "terminateEventDefinition" => "Terminate",
                    null => null,
                    var other => other
                };
                var eventRef = eventType switch
                {
                    "Message" => (string?)definition?.Attribute("messageRef"),
                    "Signal" => (string?)definition?.Attribute("signalRef"),
                    "Compensation" => (string?)definition?.Attribute("activityRef"),
                    "Error" => (string?)definition?.Attribute("errorRef"),
                    "Escalation" => (string?)definition?.Attribute("escalationRef"),
                    _ => null
                };
                var eventName = eventType switch
                {
                    "Message" when eventRef is not null && messages.TryGetValue(eventRef, out var name) => name,
                    "Signal" when eventRef is not null && signals.TryGetValue(eventRef, out var name) => name,
                    "Compensation" => eventRef,
                    "Error" => eventRef,
                    "Escalation" => eventRef,
                    _ => null
                };
                var parentSubprocess = element.Ancestors().FirstOrDefault(ancestor => ancestor.Name.LocalName is "subProcess" or "transaction");
                var attributes = element.Attributes().ToDictionary(
                    attribute => attribute.Name.LocalName,
                    attribute => attribute.Value,
                    StringComparer.OrdinalIgnoreCase);
                var activationCondition = element.Elements().FirstOrDefault(child =>
                    child.Name.LocalName == "activationCondition")?.Value.Trim();
                if (!string.IsNullOrWhiteSpace(activationCondition))
                    attributes["activationCondition"] = activationCondition;
                if (element.Name.LocalName == "businessRuleTask")
                    ReadDecisionBinding(element, attributes);
                var multiInstance = element.Elements().FirstOrDefault(child =>
                    child.Name.LocalName == "multiInstanceLoopCharacteristics");
                var loopCardinality = multiInstance?.Elements().FirstOrDefault(child =>
                    child.Name.LocalName == "loopCardinality")?.Value;
                var collection = multiInstance?.Attributes().FirstOrDefault(attribute =>
                    attribute.Name.LocalName is "collection" or "inputCollection")?.Value;
                var elementVariable = multiInstance?.Attributes().FirstOrDefault(attribute =>
                    attribute.Name.LocalName is "elementVariable" or "inputElement")?.Value;
                var outputCollection = multiInstance?.Attributes().FirstOrDefault(attribute =>
                    attribute.Name.LocalName == "outputCollection")?.Value;
                var completionCondition = multiInstance?.Elements().FirstOrDefault(child =>
                    child.Name.LocalName == "completionCondition")?.Value;
                if (!string.IsNullOrWhiteSpace(loopCardinality)) attributes["loopCardinality"] = loopCardinality;
                if (!string.IsNullOrWhiteSpace(collection)) attributes["multiInstanceCollection"] = collection;
                if (multiInstance is not null)
                    attributes["multiInstanceSequential"] =
                        string.Equals((string?)multiInstance.Attribute("isSequential"), "true", StringComparison.OrdinalIgnoreCase)
                            ? "true"
                            : "false";
                if (!string.IsNullOrWhiteSpace(elementVariable))
                    attributes["multiInstanceElementVariable"] = elementVariable;
                if (!string.IsNullOrWhiteSpace(outputCollection))
                    attributes["multiInstanceOutputCollection"] = outputCollection;
                if (!string.IsNullOrWhiteSpace(completionCondition))
                    attributes["multiInstanceCompletionCondition"] = completionCondition;
                nodes[id] = new ExecutionNode(
                    id,
                    element.Name.LocalName,
                    (string?)element.Attribute("name") ?? id,
                    (string?)element.Attribute("implementation")
                    ?? attributes.GetValueOrDefault("type")
                    ?? attributes.GetValueOrDefault("taskDefinitionType"),
                    (string?)element.Attribute("attachedToRef"),
                    (string?)parentSubprocess?.Attribute("id"),
                    eventType,
                    eventName,
                    definition?.Elements().FirstOrDefault(child => child.Name.LocalName == "timeDate")?.Value,
                    definition?.Elements().FirstOrDefault(child => child.Name.LocalName == "timeDuration")?.Value,
                    definition?.Elements().FirstOrDefault(child => child.Name.LocalName == "timeCycle")?.Value,
                    attributes);
            }

            var outgoing = new Dictionary<string, List<PendingNode>>(StringComparer.Ordinal);
            var incoming = new Dictionary<string, List<PendingNode>>(StringComparer.Ordinal);
            foreach (var flow in process.Descendants().Where(element => element.Name.LocalName == "sequenceFlow"))
            {
                var source = (string?)flow.Attribute("sourceRef");
                var target = (string?)flow.Attribute("targetRef");
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) continue;
                if (!outgoing.TryGetValue(source, out var targets)) outgoing[source] = targets = [];
                var flowId = (string?)flow.Attribute("id");
                var conditionElement = flow.Elements().FirstOrDefault(element =>
                    element.Name.LocalName == "conditionExpression");
                var condition = conditionElement?.Value;
                var conditionLanguage = (string?)conditionElement?.Attribute("language")
                                        ?? (string?)process.Document?.Root?.Attribute("expressionLanguage");
                var isDefault = nodes.TryGetValue(source, out var sourceNode)
                                && !string.IsNullOrWhiteSpace(flowId)
                                && sourceNode.Attributes.TryGetValue("default", out var defaultFlowId)
                                && string.Equals(flowId, defaultFlowId, StringComparison.Ordinal);
                var pending = new PendingNode(
                    target,
                    source,
                    flowId,
                    condition,
                    isDefault,
                    conditionLanguage);
                targets.Add(pending);
                if (!incoming.TryGetValue(target, out var incomingFlows))
                    incoming[target] = incomingFlows = [];
                incomingFlows.Add(pending);
            }

            var boundaries = nodes.Values
                .Where(node => node.Kind == "boundaryEvent" && !string.IsNullOrWhiteSpace(node.AttachedToRef))
                .GroupBy(node => node.AttachedToRef!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            var associationTargets = process.Descendants()
                .Where(element => element.Name.LocalName == "association")
                .Select(element => new
                {
                    Source = (string?)element.Attribute("sourceRef"),
                    Target = (string?)element.Attribute("targetRef")
                })
                .Where(association => !string.IsNullOrWhiteSpace(association.Source)
                                      && !string.IsNullOrWhiteSpace(association.Target))
                .GroupBy(association => association.Source!, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(association => association.Target!).ToList(),
                    StringComparer.Ordinal);
            return new ExecutionModel(nodes, outgoing, incoming, boundaries, associationTargets);
        }

        private static void ReadDecisionBinding(XElement task, IDictionary<string, string> attributes)
        {
            var binding = task.Descendants().FirstOrDefault(element =>
                element.Name.LocalName is "calledDecision" or "decision");
            var decisionRef = binding?.Attributes().FirstOrDefault(attribute =>
                                  attribute.Name.LocalName is "decisionId" or "decisionRef")?.Value
                              ?? ReadHeader(task, "decisionRef");
            var resultVariable = binding?.Attributes().FirstOrDefault(attribute =>
                                     attribute.Name.LocalName == "resultVariable")?.Value
                                 ?? ReadHeader(task, "resultVariable");

            if (!string.IsNullOrWhiteSpace(decisionRef)) attributes["decisionRef"] = decisionRef;
            if (!string.IsNullOrWhiteSpace(resultVariable)) attributes["resultVariable"] = resultVariable;
        }

        private static string? ReadHeader(XElement task, string key) =>
            task.Descendants().FirstOrDefault(element =>
                element.Name.LocalName == "header"
                && string.Equals(
                    element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "key")?.Value,
                    key,
                    StringComparison.OrdinalIgnoreCase))
            ?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "value")?.Value;

        private static bool IsFlowNode(XElement element)
            => element.Name.LocalName is "startEvent" or "endEvent" or "intermediateCatchEvent"
                or "intermediateThrowEvent" or "boundaryEvent" or "serviceTask" or "userTask"
                or "scriptTask" or "task" or "manualTask" or "businessRuleTask" or "sendTask"
                or "receiveTask" or "exclusiveGateway" or "inclusiveGateway" or "parallelGateway"
                or "eventBasedGateway" or "complexGateway" or "callActivity"
                or "subProcess" or "transaction";
    }
}
