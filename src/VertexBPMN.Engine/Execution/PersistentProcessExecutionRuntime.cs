using System.Globalization;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
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
    private const int MaxAutomaticSteps = 10_000;

    private readonly BpmnDbContext _db;
    private readonly IServiceTaskRegistry _serviceTasks;
    private readonly ILogger<PersistentProcessExecutionRuntime> _logger;

    public PersistentProcessExecutionRuntime(
        BpmnDbContext db,
        IServiceTaskRegistry serviceTasks,
        ILogger<PersistentProcessExecutionRuntime> logger)
    {
        _db = db;
        _serviceTasks = serviceTasks;
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
        await CompleteWaitingTokenAsync(instance.Id, task.ActivityId, cancellationToken);
        await CancelBoundaryJobsAsync(instance.Id, task.Id, cancellationToken);
        if (model.Nodes.TryGetValue(task.ActivityId, out var completedNode))
            await RegisterCompensationAsync(instance, completedNode, model, cancellationToken);
        AddHistory(instance, "USER_TASK_COMPLETED", task.ActivityId, new { task.Id });
        AddOutbox(instance, "UserTaskCompleted", new { task.Id, task.ActivityId });
        await AdvanceAsync(instance, model, model.Outgoing(task.ActivityId), cancellationToken);
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
            if (payload.TaskId.HasValue)
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
            if (!string.IsNullOrWhiteSpace(payload.AttachedActivityId))
                await CompleteWaitingTokenAsync(instance.Id, payload.AttachedActivityId, cancellationToken);
        }
        else
        {
            await CompleteWaitingTokenAsync(instance.Id, job.ActivityId, cancellationToken);
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
        subscription.State = "Consumed";
        subscription.ConsumedAt = DateTime.UtcNow;
        subscription.Revision++;
        var token = await _db.ExecutionTokens.SingleAsync(
            item => item.Id == subscription.ExecutionTokenId,
            cancellationToken);
        token.State = ExecutionToken.CompletedState;
        token.Revision++;
        AddHistory(instance, $"{subscription.EventType.ToUpperInvariant()}_CORRELATED", subscription.ActivityId,
            new { subscription.EventName });
        await AdvanceAsync(instance, model, model.Outgoing(subscription.ActivityId), cancellationToken);
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

            switch (node.Kind)
            {
                case "startEvent":
                case "exclusiveGateway":
                case "inclusiveGateway":
                    Enqueue(queue, model.Outgoing(node.Id));
                    break;

                case "parallelGateway":
                    if (model.IncomingCount(node.Id) > 1 && model.OutgoingCount(node.Id) <= 1)
                    {
                        if (await ArriveParallelJoinAsync(instance, node, pending.SourceNodeId, model, cancellationToken))
                            Enqueue(queue, model.Outgoing(node.Id));
                    }
                    else
                    {
                        Enqueue(queue, model.Outgoing(node.Id));
                    }
                    break;

                case "serviceTask":
                    await ExecuteServiceTaskAsync(instance, node, cancellationToken);
                    if (instance.Status == ProcessInstanceStatus.Running)
                    {
                        await RegisterCompensationAsync(instance, node, model, cancellationToken);
                        Enqueue(queue, model.Outgoing(node.Id));
                    }
                    break;

                case "userTask":
                    await CreateUserTaskWaitAsync(instance, node, model, cancellationToken);
                    break;

                case "subProcess":
                case "transaction":
                    if (node.Attributes.ContainsKey("multiInstanceCollection")
                        || node.Attributes.ContainsKey("loopCardinality"))
                    {
                        throw new NotSupportedException(
                            $"Multi-instance subprocess '{node.Id}' requires execution semantics that are not part of the production subset.");
                    }

                    var subprocessStarts = model.SubprocessStartNodes(node.Id).ToArray();
                    if (subprocessStarts.Length == 0)
                    {
                        // An empty embedded subprocess represents one successful execution.
                        Enqueue(queue, model.Outgoing(node.Id));
                    }
                    else
                    {
                        Enqueue(queue, subprocessStarts);
                    }
                    break;

                case "intermediateCatchEvent":
                    await CreateEventWaitAsync(instance, node, cancellationToken);
                    break;

                case "endEvent":
                    if (node.EventType == "Compensation")
                        await TriggerCompensationAsync(instance, node, model, queue, cancellationToken);
                    AddHistory(instance, "END_EVENT_REACHED", node.Id, new { node.EventType });
                    if (!string.IsNullOrWhiteSpace(node.ParentSubprocessId))
                        Enqueue(queue, model.Outgoing(node.ParentSubprocessId));
                    break;

                case "intermediateThrowEvent":
                    if (node.EventType != "Compensation")
                        throw new NotSupportedException($"Throw event '{node.Id}' has unsupported definition '{node.EventType}'.");
                    await TriggerCompensationAsync(instance, node, model, queue, cancellationToken);
                    Enqueue(queue, model.Outgoing(node.Id));
                    break;

                case "task":
                case "manualTask":
                case "businessRuleTask":
                case "sendTask":
                case "receiveTask":
                    Enqueue(queue, model.Outgoing(node.Id));
                    break;

                case "scriptTask":
                    throw new InvalidOperationException("In-process script task execution is disabled.");

                default:
                    throw new NotSupportedException($"Flow node type '{node.Kind}' is not supported by the production subset.");
            }
        }
    }

    private async Task ExecuteServiceTaskAsync(
        ProcessInstance instance,
        ExecutionNode node,
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
            await handler.ExecuteAsync(node.Attributes, instance.Variables, cancellationToken);
            instance.Variables = new Dictionary<string, object>(instance.Variables, StringComparer.Ordinal);
            AddHistory(instance, "SERVICE_TASK_COMPLETED", node.Id, new { node.Implementation });
            AddOutbox(instance, "ServiceTaskCompleted", new { node.Id, node.Implementation });
        }
        catch (Exception exception)
        {
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

    private async Task CreateUserTaskWaitAsync(
        ProcessInstance instance,
        ExecutionNode node,
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
            Revision = 1
        };
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
                    AttachedActivityId = node.Id
                })
            });
        }
        await Task.CompletedTask;
    }

    private Task CreateEventWaitAsync(
        ProcessInstance instance,
        ExecutionNode node,
        CancellationToken cancellationToken)
    {
        var token = CreateWaitingToken(instance, node);
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
                    CreatedAt = DateTime.UtcNow,
                    Revision = 1
                });
                break;
            default:
                throw new NotSupportedException($"Catch event '{node.Id}' has unsupported definition '{node.EventType}'.");
        }
        AddHistory(instance, "WAIT_STATE_CREATED", node.Id, new { node.EventType, node.EventName });
        return Task.CompletedTask;
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

    private async Task<bool> ArriveParallelJoinAsync(
        ProcessInstance instance,
        ExecutionNode node,
        string? sourceNodeId,
        ExecutionModel model,
        CancellationToken cancellationToken)
    {
        var source = sourceNodeId ?? string.Empty;
        var arrivals = await _db.ExecutionTokens
            .Where(token => token.ProcessInstanceId == instance.Id
                            && token.CurrentNodeId == node.Id
                            && token.State == ExecutionToken.WaitingState)
            .ToListAsync(cancellationToken);
        var arrivedSources = arrivals
            .Select(token => token.Variables.TryGetValue("joinSource", out var value) ? value?.ToString() : null)
            .Where(value => !string.IsNullOrEmpty(value))
            .ToHashSet(StringComparer.Ordinal);
        arrivedSources.Add(source);

        if (arrivedSources.Count < model.IncomingCount(node.Id))
        {
            if (!arrivals.Any(token => token.Variables.TryGetValue("joinSource", out var value)
                                       && string.Equals(value?.ToString(), source, StringComparison.Ordinal)))
            {
                var token = CreateWaitingToken(instance, node);
                token.Variables["joinSource"] = source;
            }
            return false;
        }

        foreach (var arrival in arrivals)
        {
            arrival.State = ExecutionToken.CompletedState;
            arrival.Revision++;
        }
        AddHistory(instance, "PARALLEL_JOIN_COMPLETED", node.Id, new { incoming = arrivedSources.Count });
        return true;
    }

    private async Task RegisterCompensationAsync(
        ProcessInstance instance,
        ExecutionNode completedNode,
        ExecutionModel model,
        CancellationToken cancellationToken)
    {
        foreach (var boundary in model.BoundaryEvents(completedNode.Id).Where(item => item.EventType == "Compensation"))
        {
            var exists = await _db.EventSubscriptions.AnyAsync(subscription =>
                subscription.ProcessInstanceId == instance.Id
                && subscription.ActivityId == boundary.Id
                && subscription.State == ActiveSubscription,
                cancellationToken);
            if (exists) continue;

            var token = new ExecutionToken
            {
                Id = Guid.NewGuid(),
                ProcessInstanceId = instance.Id,
                CurrentNodeId = boundary.Id,
                NodeType = "compensationBoundary",
                Variables = new Dictionary<string, object>(instance.Variables, StringComparer.Ordinal),
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
                ActivityId = boundary.Id,
                EventType = "Compensation",
                EventName = completedNode.Id,
                TenantId = instance.TenantId,
                State = ActiveSubscription,
                CreatedAt = DateTime.UtcNow,
                Revision = 1
            });
            AddHistory(instance, "COMPENSATION_REGISTERED", completedNode.Id, new { boundary.Id });
        }
    }

    private async Task TriggerCompensationAsync(
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

        var subscriptions = await query.OrderByDescending(subscription => subscription.CreatedAt)
            .ToListAsync(cancellationToken);
        foreach (var subscription in subscriptions)
        {
            subscription.State = "Consumed";
            subscription.ConsumedAt = DateTime.UtcNow;
            subscription.Revision++;
            var token = await _db.ExecutionTokens.SingleAsync(
                item => item.Id == subscription.ExecutionTokenId, cancellationToken);
            token.State = ExecutionToken.CompletedState;
            token.Revision++;
            Enqueue(queue, model.Outgoing(subscription.ActivityId));
        }
        AddHistory(instance, "COMPENSATION_THROWN", throwEvent.Id,
            new { activityRef = throwEvent.EventName, handlers = subscriptions.Count });
        AddOutbox(instance, "CompensationTriggered",
            new { throwEvent.Id, activityRef = throwEvent.EventName, handlers = subscriptions.Count });
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
        var activeTokens = await _db.ExecutionTokens.AsNoTracking()
            .Where(token => token.ProcessInstanceId == instance.Id && token.State == ExecutionToken.WaitingState)
            .Select(token => token.CurrentNodeId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
        var activeTasks = await _db.Tasks.AsNoTracking()
            .Where(task => task.ProcessInstanceId == instance.Id
                           && (task.Status == UserTaskStatus.Pending || task.Status == UserTaskStatus.Delegated))
            .Select(task => task.ActivityId)
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
        var activeJobs = await _db.Jobs.AsNoTracking()
            .AnyAsync(job => job.ProcessInstanceId == instance.Id && job.State == ScheduledJob, cancellationToken);
        var activeSubscriptions = await _db.EventSubscriptions.AsNoTracking()
            .AnyAsync(subscription => subscription.ProcessInstanceId == instance.Id
                                      && subscription.State == ActiveSubscription
                                      && subscription.EventType != "Compensation", cancellationToken);

        instance.ActiveTokens = activeTokens;
        instance.ActiveTasks = activeTasks;
        instance.LastModified = DateTime.UtcNow;
        instance.Revision++;
        if (instance.Status == ProcessInstanceStatus.Running
            && activeTokens.Count == 0
            && activeTasks.Count == 0
            && !activeJobs
            && !activeSubscriptions)
        {
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

    private static void Enqueue(Queue<PendingNode> queue, IEnumerable<PendingNode> nodes)
    {
        foreach (var node in nodes) queue.Enqueue(node);
    }

    private sealed class TimerPayload
    {
        public string Kind { get; set; } = "catch";
        public Guid? TaskId { get; set; }
        public string? AttachedActivityId { get; set; }
    }

    private sealed record PendingNode(string NodeId, string? SourceNodeId);

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
        Dictionary<string, string> Attributes);

    private sealed class ExecutionModel
    {
        private readonly Dictionary<string, List<PendingNode>> _outgoing;
        private readonly Dictionary<string, int> _incoming;
        private readonly Dictionary<string, List<ExecutionNode>> _boundaries;

        private ExecutionModel(
            Dictionary<string, ExecutionNode> nodes,
            Dictionary<string, List<PendingNode>> outgoing,
            Dictionary<string, int> incoming,
            Dictionary<string, List<ExecutionNode>> boundaries)
        {
            Nodes = nodes;
            _outgoing = outgoing;
            _incoming = incoming;
            _boundaries = boundaries;
        }

        public IReadOnlyDictionary<string, ExecutionNode> Nodes { get; }
        public IEnumerable<PendingNode> Outgoing(string nodeId)
            => _outgoing.TryGetValue(nodeId, out var nodes) ? nodes : [];
        public int IncomingCount(string nodeId) => _incoming.GetValueOrDefault(nodeId);
        public int OutgoingCount(string nodeId) => _outgoing.GetValueOrDefault(nodeId)?.Count ?? 0;
        public IEnumerable<ExecutionNode> BoundaryEvents(string activityId)
            => _boundaries.TryGetValue(activityId, out var nodes) ? nodes : [];
        public IEnumerable<PendingNode> SubprocessStartNodes(string subprocessId)
            => Nodes.Values
                .Where(node => node.Kind == "startEvent"
                               && string.Equals(node.ParentSubprocessId, subprocessId, StringComparison.Ordinal))
                .Select(node => new PendingNode(node.Id, subprocessId));

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
                    null => null,
                    var other => other
                };
                var eventRef = eventType switch
                {
                    "Message" => (string?)definition?.Attribute("messageRef"),
                    "Signal" => (string?)definition?.Attribute("signalRef"),
                    "Compensation" => (string?)definition?.Attribute("activityRef"),
                    _ => null
                };
                var eventName = eventType switch
                {
                    "Message" when eventRef is not null && messages.TryGetValue(eventRef, out var name) => name,
                    "Signal" when eventRef is not null && signals.TryGetValue(eventRef, out var name) => name,
                    "Compensation" => eventRef,
                    _ => null
                };
                var parentSubprocess = element.Ancestors().FirstOrDefault(ancestor => ancestor.Name.LocalName is "subProcess" or "transaction");
                var attributes = element.Attributes().ToDictionary(
                    attribute => attribute.Name.LocalName,
                    attribute => attribute.Value,
                    StringComparer.OrdinalIgnoreCase);
                var multiInstance = element.Elements().FirstOrDefault(child =>
                    child.Name.LocalName == "multiInstanceLoopCharacteristics");
                var loopCardinality = multiInstance?.Elements().FirstOrDefault(child =>
                    child.Name.LocalName == "loopCardinality")?.Value;
                var collection = multiInstance?.Attributes().FirstOrDefault(attribute =>
                    attribute.Name.LocalName == "collection")?.Value;
                if (!string.IsNullOrWhiteSpace(loopCardinality)) attributes["loopCardinality"] = loopCardinality;
                if (!string.IsNullOrWhiteSpace(collection)) attributes["multiInstanceCollection"] = collection;
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
            var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var flow in process.Descendants().Where(element => element.Name.LocalName == "sequenceFlow"))
            {
                var source = (string?)flow.Attribute("sourceRef");
                var target = (string?)flow.Attribute("targetRef");
                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) continue;
                if (!outgoing.TryGetValue(source, out var targets)) outgoing[source] = targets = [];
                targets.Add(new PendingNode(target, source));
                incoming[target] = incoming.GetValueOrDefault(target) + 1;
            }

            var boundaries = nodes.Values
                .Where(node => node.Kind == "boundaryEvent" && !string.IsNullOrWhiteSpace(node.AttachedToRef))
                .GroupBy(node => node.AttachedToRef!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
            return new ExecutionModel(nodes, outgoing, incoming, boundaries);
        }

        private static bool IsFlowNode(XElement element)
            => element.Name.LocalName is "startEvent" or "endEvent" or "intermediateCatchEvent"
                or "intermediateThrowEvent" or "boundaryEvent" or "serviceTask" or "userTask"
                or "scriptTask" or "task" or "manualTask" or "businessRuleTask" or "sendTask"
                or "receiveTask" or "exclusiveGateway" or "inclusiveGateway" or "parallelGateway"
                or "subProcess" or "transaction";
    }
}
