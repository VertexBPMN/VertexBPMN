using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using System.Text.Json;

namespace VertexBPMN.Engine.Execution;

public sealed partial class PersistentProcessExecutionRuntime
{
    private async Task ValidateCalledExternalCapabilitiesAsync(ExecutionModel model, string? tenantId, CancellationToken cancellationToken)
    {
        var models = new Stack<ExecutionModel>();
        var visited = new HashSet<Guid>();
        models.Push(model);
        while (models.TryPop(out var current))
        {
            foreach (var call in current.Nodes.Values.Where(node => node.Kind == "callActivity"))
            {
                if (!call.Attributes.TryGetValue("calledElement", out var key) || string.IsNullOrWhiteSpace(key)) continue;
                var definition = await _db.ProcessDefinitions.AsNoTracking()
                    .Where(item => item.TenantScope == TenantScope(tenantId) && item.Key == key)
                    .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
                if (definition is null || !visited.Add(definition.Id)) continue;
                if (visited.Count > 64) throw new InvalidOperationException("external_task_call_graph_limit");
                var child = ExecutionModel.Parse(definition.BpmnXml, definition.Key);
                if (child.Nodes.Values.Any(node => Domain.Model.Bpmn.ExternalTaskDefinition.FromAttributes(node.Attributes) is not null))
                    throw new InvalidOperationException("external_task_feature_not_enabled");
                models.Push(child);
            }
        }
    }

    private async Task RegisterExternalBoundariesAsync(ProcessInstance instance, ExecutionNode node,
        ExecutionModel model, CancellationToken cancellationToken)
    {
        foreach (var boundary in model.BoundaryEvents(node.Id))
        {
            if (boundary.EventType == "Timer")
            {
                // A boundary belongs to the activity as a whole, not each MI iteration.
                var timers = await _db.Jobs.Where(job => job.ProcessInstanceId == instance.Id && job.ActivityId == boundary.Id && job.State == ScheduledJob)
                    .ToListAsync(cancellationToken);
                if (timers.Concat(_db.Jobs.Local).Any(job => job.ProcessInstanceId == instance.Id && job.ActivityId == boundary.Id && job.State == ScheduledJob))
                    continue;
                _db.Jobs.Add(new Job
                {
                    Id = Guid.NewGuid(), ProcessInstanceId = instance.Id, ActivityId = boundary.Id,
                    Type = "timer", State = ScheduledJob, DueDate = ResolveDueDate(boundary),
                    TenantId = instance.TenantId, CreatedAt = _timeProvider.GetUtcNow().UtcDateTime, Revision = 1,
                    Payload = JsonSerializer.Serialize(new TimerPayload
                    { Kind = "boundary", AttachedActivityId = node.Id, Interrupting = IsBoundaryInterrupting(boundary) })
                });
            }
            else if (boundary.EventType is "Message" or "Signal")
            {
                var key = ActiveSubscriptionKey(instance.Id, boundary.Id);
                var subscriptions = await _db.EventSubscriptions.Where(item => item.ActiveKey == key).ToListAsync(cancellationToken);
                if (subscriptions.Concat(_db.EventSubscriptions.Local).Any(item => item.ActiveKey == key)) continue;
                var token = CreateWaitingToken(instance, boundary);
                // A boundary wait must not copy all private process inputs into its token.
                token.Variables.Clear();
                _db.EventSubscriptions.Add(new EventSubscription
                {
                    Id = Guid.NewGuid(), ProcessInstanceId = instance.Id, ExecutionTokenId = token.Id,
                    ActivityId = boundary.Id, EventType = boundary.EventType,
                    EventName = boundary.EventName ?? throw new InvalidOperationException("external_task_boundary_name_missing"),
                    TenantId = instance.TenantId, State = ActiveSubscription, ActiveKey = key,
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime, Revision = 1
                });
            }
        }
    }

    private async Task CancelExternalActivityAsync(Guid processId, string activityId, ExecutionModel model,
        CancellationToken cancellationToken)
    {
        if (!model.Nodes.TryGetValue(activityId, out var node)) return;
        if (node.Kind is "subProcess" or "transaction"
            && model.Nodes.Values.Any(candidate => model.IsInScope(candidate.Id, activityId)
                && Domain.Model.Bpmn.ExternalTaskDefinition.FromAttributes(candidate.Attributes) is not null))
            await CancelWaitStatesAsync(processId, id => model.IsInScope(id, activityId), cancellationToken);
        else if (Domain.Model.Bpmn.ExternalTaskDefinition.FromAttributes(node.Attributes) is not null)
            await CancelExternalTaskWaitsAsync(processId, id => id == activityId, cancellationToken);
        else return;
        await CancelBoundarySubscriptionsAsync(processId, model, activityId, cancellationToken);
        var boundaryIds = model.BoundaryEvents(activityId).Select(item => item.Id).ToArray();
        var timers = await _db.Jobs.Where(item => item.ProcessInstanceId == processId
            && boundaryIds.Contains(item.ActivityId) && item.State == ScheduledJob).ToListAsync(cancellationToken);
        foreach (var timer in timers)
        {
            timer.State = "Cancelled";
            timer.CompletedAt = _timeProvider.GetUtcNow().UtcDateTime;
            timer.LockOwner = null;
            timer.LockedUntil = null;
            timer.Revision++;
        }
    }

    private async Task CancelExternalTaskWaitsAsync(Guid processInstanceId, Func<string, bool> belongsToScope,
        CancellationToken cancellationToken)
    {
        var persisted = await _db.ExternalTaskJobs.Where(job => job.ProcessInstanceId == processInstanceId)
            .ToListAsync(cancellationToken);
        var jobs = _db.ExternalTaskJobs.Local.Where(job => job.ProcessInstanceId == processInstanceId)
            .Concat(persisted).DistinctBy(job => job.Id).Where(job => belongsToScope(job.ActivityId)).ToArray();
        if (jobs.Length == 0) return;
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("external_task_transaction_required");
        var now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var jobIds = jobs.Select(job => job.Id).ToArray();
        var multiInstanceIds = jobs.Where(job => job.MultiInstanceExecutionId.HasValue)
            .Select(job => job.MultiInstanceExecutionId!.Value).Distinct().ToArray();
        var executions = await _db.MultiInstanceExecutions.Where(item => multiInstanceIds.Contains(item.Id) && item.State == "Active")
            .ToListAsync(cancellationToken);
        foreach (var execution in executions.Concat(_db.MultiInstanceExecutions.Local).DistinctBy(item => item.Id)
                     .Where(item => multiInstanceIds.Contains(item.Id) && item.State == "Active"))
        {
            execution.State = "Cancelled";
            execution.Revision++;
        }
        var attempts = await _db.ExternalTaskAttempts.Where(attempt => jobIds.Contains(attempt.JobId) && attempt.EndedAt == null)
            .ToListAsync(cancellationToken);
        var continuations = await _db.ExternalTaskContinuations.Where(item => jobIds.Contains(item.JobId)
            && item.State == ExternalTaskContinuationState.Pending).ToListAsync(cancellationToken);
        foreach (var job in jobs)
        {
            if (job.State is not (ExternalTaskState.Ready or ExternalTaskState.Leased or ExternalTaskState.RetryScheduled))
                continue;
            job.State = ExternalTaskState.Cancelled;
            job.CompletedAt = now;
            job.LeaseId = null;
            job.LeaseExpiresAt = null;
            job.WorkerIssuer = null;
            job.WorkerSubject = null;
            job.ErrorCode = "process_scope_cancelled";
            job.Revision++;
            foreach (var attempt in attempts.Concat(_db.ExternalTaskAttempts.Local).DistinctBy(item => item.Id)
                         .Where(item => item.JobId == job.Id && item.EndedAt == null))
            {
                attempt.EndedAt = now;
                attempt.EndReason = "Cancelled";
            }
            var instance = _db.ProcessInstances.Local.FirstOrDefault(item => item.Id == processInstanceId)
                ?? await _db.ProcessInstances.SingleAsync(item => item.Id == processInstanceId, cancellationToken);
            AddHistory(instance, "EXTERNAL_TASK_CANCELLED", job.ActivityId, new { jobId = job.Id, job.ActivityExecutionId });
        }
        foreach (var continuation in continuations.Concat(_db.ExternalTaskContinuations.Local).DistinctBy(item => item.Id)
                     .Where(item => jobIds.Contains(item.JobId) && item.State == ExternalTaskContinuationState.Pending))
        {
            continuation.State = ExternalTaskContinuationState.Cancelled;
            continuation.Revision++;
        }
    }
}
