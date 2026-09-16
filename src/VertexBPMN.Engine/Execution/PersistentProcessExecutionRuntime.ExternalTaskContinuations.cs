using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Engine.Execution;

public sealed partial class PersistentProcessExecutionRuntime
{
    public async ValueTask<ExternalTaskContinuationBatchResult> ProcessExternalTaskContinuationsAsync(
        int maximumItems = 100, CancellationToken cancellationToken = default)
    {
        if (maximumItems is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maximumItems));
        var continuationIds = await _db.ExternalTaskContinuations.AsNoTracking()
            .Where(item => item.State == ExternalTaskContinuationState.Pending)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => item.Id).Take(maximumItems).ToListAsync(cancellationToken);
        var applied = 0;
        var conflicts = 0;
        foreach (var id in continuationIds)
        {
            if (await ApplyExternalContinuationAsync(id, cancellationToken)) applied++; else conflicts++;
            _db.ChangeTracker.Clear();
        }

        var dispatchIds = await _db.ExecutionTokens.AsNoTracking()
            .Where(item => item.NodeType == ExternalDispatchNodeType && item.State == ExecutionToken.PendingState)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => item.Id).Take(maximumItems).ToListAsync(cancellationToken);
        var dispatched = 0;
        foreach (var id in dispatchIds)
        {
            if (await DispatchExternalContinuationAsync(id, cancellationToken)) dispatched++; else conflicts++;
            _db.ChangeTracker.Clear();
        }
        return new ExternalTaskContinuationBatchResult(applied, dispatched, conflicts);
    }

    private async ValueTask<bool> ApplyExternalContinuationAsync(Guid continuationId,
        CancellationToken cancellationToken)
    {
        var metadata = await (from continuation in _db.ExternalTaskContinuations.AsNoTracking()
            join job in _db.ExternalTaskJobs.AsNoTracking() on continuation.JobId equals job.Id
            where continuation.Id == continuationId
            select new { continuation.JobId, job.ProcessInstanceId, job.WaitTokenId }).SingleOrDefaultAsync(cancellationToken);
        if (metadata is null) return false;
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var instance = await _db.ProcessInstances.SingleOrDefaultAsync(item => item.Id == metadata.ProcessInstanceId, cancellationToken);
            var wait = await _db.ExecutionTokens.SingleOrDefaultAsync(item => item.Id == metadata.WaitTokenId, cancellationToken);
            var job = await _db.ExternalTaskJobs.SingleOrDefaultAsync(item => item.Id == metadata.JobId, cancellationToken);
            var continuation = await _db.ExternalTaskContinuations.SingleOrDefaultAsync(item =>
                item.Id == continuationId && item.State == ExternalTaskContinuationState.Pending, cancellationToken);
            if (continuation is null) return false;
            if (instance?.Status != ProcessInstanceStatus.Running || wait?.State != ExecutionToken.WaitingState
                || job is null || wait.ActivityExecutionId != job.ActivityExecutionId
                || continuation.ActivityExecutionId != job.ActivityExecutionId)
            {
                continuation.State = ExternalTaskContinuationState.Cancelled;
                continuation.Revision++;
                await _db.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return true;
            }

            var definition = await _db.ProcessDefinitions.AsNoTracking()
                .SingleAsync(item => item.Id == instance.ProcessDefinitionId, cancellationToken);
            var model = ExecutionModel.Parse(definition.BpmnXml, definition.Key);
            if (!model.Nodes.TryGetValue(job.ActivityId, out var node))
                throw new InvalidOperationException("external_task_activity_missing");
            var context = ExecutionContextFromToken(wait);
            if (job.MultiInstanceExecutionId.HasValue)
                context = context with
                {
                    MultiInstanceExecutionId = job.MultiInstanceExecutionId,
                    MultiInstanceIndex = job.MultiInstanceIndex,
                    MultiInstanceActivityId = node.Id
                };
            wait.State = ExecutionToken.CompletedState;
            wait.Revision++;
            var queue = new Queue<PendingNode>();
            if (continuation.Outcome == ExternalTaskOutcome.Success)
            {
                context = ApplyExternalOutput(job, node, context, instance);
                await RegisterCompensationAsync(instance, node, model, cancellationToken);
                await QueueSuccessfulExternalContinuationAsync(instance, node, context, model, queue, cancellationToken);
                if (!job.MultiInstanceExecutionId.HasValue
                    || !await _db.MultiInstanceExecutions.AnyAsync(item =>
                        item.Id == job.MultiInstanceExecutionId.Value && item.State == "Active", cancellationToken))
                    await CancelExternalBoundaryWaitsAsync(instance.Id, job.ActivityId, model, cancellationToken);
            }
            else
            {
                await CancelExternalActivityAsync(instance.Id, job.ActivityId, model, cancellationToken);
                var errorCode = continuation.Outcome switch
                {
                    ExternalTaskOutcome.BusinessError => job.ErrorCode,
                    ExternalTaskOutcome.Timeout => "external_task_timeout",
                    _ => "external_task_failed"
                };
                var boundary = model.BoundaryEvents(job.ActivityId).FirstOrDefault(candidate =>
                    candidate.EventType == "Error" && (string.IsNullOrWhiteSpace(candidate.EventName)
                        || string.Equals(candidate.EventName, errorCode, StringComparison.Ordinal)));
                if (boundary is null)
                    await SuspendWithIncidentAsync(instance, job.ActivityId,
                        errorCode ?? "external_task_failed", cancellationToken, context);
                else
                {
                    Enqueue(queue, model.Outgoing(boundary.Id), context);
                    AddHistory(instance, "EXTERNAL_TASK_ERROR_CAUGHT", boundary.Id,
                        new { jobId = job.Id, code = errorCode });
                }
            }

            foreach (var pending in queue) AddExternalDispatchToken(instance, pending);
            continuation.State = ExternalTaskContinuationState.Applied;
            continuation.AppliedAt = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
            continuation.Revision++;
            instance.State = instance.Status == ProcessInstanceStatus.Running ? "Waiting" : instance.State;
            instance.LastModified = _timeProvider.GetUtcNow().UtcDateTime;
            instance.Revision++;
            AddHistory(instance, "EXTERNAL_TASK_CONTINUATION_APPLIED", job.ActivityId,
                new { jobId = job.Id, outcome = continuation.Outcome.ToString() });
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }

    private async ValueTask<bool> DispatchExternalContinuationAsync(Guid tokenId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _db.ExecutionTokens.SingleOrDefaultAsync(item => item.Id == tokenId
                && item.NodeType == ExternalDispatchNodeType && item.State == ExecutionToken.PendingState, cancellationToken);
            if (token is null) return false;
            var instance = await _db.ProcessInstances.SingleOrDefaultAsync(item => item.Id == token.ProcessInstanceId, cancellationToken);
            if (instance?.Status != ProcessInstanceStatus.Running)
            {
                token.State = ExecutionToken.CompletedState;
                token.Revision++;
                await _db.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return true;
            }
            var definition = await _db.ProcessDefinitions.AsNoTracking()
                .SingleAsync(item => item.Id == instance.ProcessDefinitionId, cancellationToken);
            var model = ExecutionModel.Parse(definition.BpmnXml, definition.Key);
            var pending = ExternalDispatchContext(token);
            token.State = ExecutionToken.CompletedState;
            token.Revision++;
            await AdvanceAsync(instance, model, [pending], cancellationToken);
            await FinalizeTransitionAsync(instance, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }

    private PendingNode ApplyExternalOutput(ExternalTaskJob job, ExecutionNode node,
        PendingNode context, ProcessInstance instance)
    {
        if (job.Result is null) throw new InvalidOperationException("external_task_result_missing");
        using var snapshot = JsonDocument.Parse(job.DefinitionSnapshot);
        if (!snapshot.RootElement.TryGetProperty("mappings", out var mappings)
            || !mappings.TryGetProperty("vertex:ioMapping.output.result", out var targetElement))
            return context;
        var target = targetElement.GetString();
        if (string.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("external_task_output_mapping_invalid");
        using var result = JsonDocument.Parse(job.Result);
        var value = BpmnConditionEvaluator.NormalizeJsonValue(result.RootElement.Clone()) ?? new object();
        if (!job.MultiInstanceExecutionId.HasValue && string.IsNullOrWhiteSpace(node.ParentSubprocessId))
        {
            var variables = new Dictionary<string, object>(instance.Variables, StringComparer.Ordinal)
            {
                [target] = value
            };
            instance.Variables = variables;
            _db.Entry(instance).Property(item => item.Variables).IsModified = true;
            return context;
        }
        var locals = context.LocalVariables is null ? new Dictionary<string, object>(StringComparer.Ordinal)
            : new Dictionary<string, object>(context.LocalVariables, StringComparer.Ordinal);
        locals[target] = value;
        return context with { LocalVariables = locals };
    }

    private async Task QueueSuccessfulExternalContinuationAsync(ProcessInstance instance,
        ExecutionNode node, PendingNode context, ExecutionModel model, Queue<PendingNode> queue,
        CancellationToken cancellationToken)
    {
        if (!node.Attributes.ContainsKey("standardLoop"))
        {
            await CompleteActivityAsync(instance, node, context, model, queue, cancellationToken);
            return;
        }
        var locals = context.LocalVariables is null ? new Dictionary<string, object>(StringComparer.Ordinal)
            : new Dictionary<string, object>(context.LocalVariables, StringComparer.Ordinal);
        var current = locals.TryGetValue("loopCounter", out var raw)
            ? Convert.ToInt32(BpmnConditionEvaluator.NormalizeJsonValue(raw), CultureInfo.InvariantCulture) : 0;
        var next = checked(current + 1);
        locals["loopCounter"] = next;
        var maximum = node.Attributes.GetValueOrDefault("standardLoopMaximum");
        var limit = maximum is null ? int.MaxValue : int.Parse(maximum, CultureInfo.InvariantCulture);
        var condition = node.Attributes.GetValueOrDefault("standardLoopCondition");
        var continueLoop = next < limit && (string.IsNullOrWhiteSpace(condition)
            || BpmnConditionEvaluator.Evaluate(condition, CreateActivityVariables(instance.Variables, locals)));
        if (continueLoop) queue.Enqueue(new PendingNode(node.Id, node.Id, LocalVariables: locals));
        else Enqueue(queue, model.Outgoing(node.Id), context with { LocalVariables = locals });
    }

    private void AddExternalDispatchToken(ProcessInstance instance, PendingNode pending)
    {
        var token = new ExecutionToken
        {
            Id = Guid.NewGuid(), ProcessInstanceId = instance.Id, CurrentNodeId = pending.NodeId,
            NodeType = ExternalDispatchNodeType, State = ExecutionToken.PendingState, Revision = 1,
            ScopeExecutionId = pending.MultiInstanceExecutionId ?? instance.Id,
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };
        StoreExecutionContext(token, pending);
        if (pending.SourceNodeId is not null) token.Variables[ExternalDispatchSourceVariable] = pending.SourceNodeId;
        if (pending.FlowId is not null) token.Variables[ExternalDispatchFlowVariable] = pending.FlowId;
        if (pending.ConditionExpression is not null) token.Variables[ExternalDispatchConditionVariable] = pending.ConditionExpression;
        token.Variables[ExternalDispatchDefaultVariable] = pending.IsDefault;
        if (pending.ConditionExpressionLanguage is not null)
            token.Variables[ExternalDispatchLanguageVariable] = pending.ConditionExpressionLanguage;
        _db.ExecutionTokens.Add(token);
    }

    private static PendingNode ExternalDispatchContext(ExecutionToken token)
    {
        var context = ExecutionContextFromToken(token);
        var locals = context.LocalVariables?.Where(pair => !pair.Key.StartsWith("$vertex.externalDispatch.", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return new PendingNode(token.CurrentNodeId,
            token.Variables.GetValueOrDefault(ExternalDispatchSourceVariable)?.ToString(),
            token.Variables.GetValueOrDefault(ExternalDispatchFlowVariable)?.ToString(),
            token.Variables.GetValueOrDefault(ExternalDispatchConditionVariable)?.ToString(),
            token.Variables.TryGetValue(ExternalDispatchDefaultVariable, out var isDefault)
                && Convert.ToBoolean(BpmnConditionEvaluator.NormalizeJsonValue(isDefault), CultureInfo.InvariantCulture),
            token.Variables.GetValueOrDefault(ExternalDispatchLanguageVariable)?.ToString(),
            context.MultiInstanceExecutionId, context.MultiInstanceIndex, locals, context.MultiInstanceActivityId);
    }
}
