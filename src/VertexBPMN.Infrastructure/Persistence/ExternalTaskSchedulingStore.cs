using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Infrastructure.Persistence;

/// <summary>
/// Stages a prevalidated job and its wait in the caller's runtime transaction.
/// Topic/schema authorization is the responsibility of the runtime before invoking this store.
/// </summary>
public sealed class ExternalTaskSchedulingStore(BpmnDbContext db)
{
    public async Task<ExternalTaskJob> StageAsync(ExternalTaskJob job, ExecutionToken wait, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("external_task_transaction_required");
        if (string.IsNullOrWhiteSpace(job.TenantId) || job.ActivityExecutionId == Guid.Empty
            || job.ScopeExecutionId == Guid.Empty || job.Id == Guid.Empty || wait.Id == Guid.Empty
            || job.WaitTokenId != wait.Id || job.ProcessInstanceId != wait.ProcessInstanceId
            || job.ActivityId != wait.CurrentNodeId || wait.ActivityExecutionId != job.ActivityExecutionId
            || wait.ScopeExecutionId != job.ScopeExecutionId || wait.State != ExecutionToken.WaitingState
            || job.State != ExternalTaskState.Ready || job.AttemptsStarted != 0 || job.LeaseId is not null
            || job.MaxAttempts is < 1 or > 10 || job.Deadline <= job.CreatedAt)
            throw new InvalidOperationException("external_task_invalid_wait");

        var owner = db.ProcessInstances.Local.FirstOrDefault(instance => instance.Id == job.ProcessInstanceId)
            ?? await db.ProcessInstances.SingleOrDefaultAsync(instance => instance.Id == job.ProcessInstanceId, cancellationToken);
        if (owner is null || owner.TenantId != job.TenantId || owner.ProcessDefinitionId != job.DefinitionId
            || owner.Status != ProcessInstanceStatus.Running)
            throw new InvalidOperationException("external_task_invalid_process");
        if (!await db.ProcessDefinitions.AnyAsync(definition => definition.Id == job.DefinitionId
                && definition.TenantId == job.TenantId && definition.Version == job.DefinitionVersion, cancellationToken))
            throw new InvalidOperationException("external_task_invalid_definition");

        var existing = db.ExternalTaskJobs.Local.FirstOrDefault(item => item.TenantId == job.TenantId && item.ActivityExecutionId == job.ActivityExecutionId)
            ?? await db.ExternalTaskJobs.SingleOrDefaultAsync(item => item.TenantId == job.TenantId && item.ActivityExecutionId == job.ActivityExecutionId, cancellationToken);
        if (existing is not null)
        {
            if (existing.ProcessInstanceId != job.ProcessInstanceId || existing.WaitTokenId != job.WaitTokenId
                || existing.DefinitionSnapshot != job.DefinitionSnapshot || existing.InputSnapshot != job.InputSnapshot
                || existing.Topic != job.Topic || existing.ContractVersion != job.ContractVersion
                || existing.SchemaSnapshot != job.SchemaSnapshot || existing.ActivityId != job.ActivityId
                || existing.ScopeExecutionId != job.ScopeExecutionId || existing.DefinitionId != job.DefinitionId
                || existing.DefinitionVersion != job.DefinitionVersion || existing.AgentProfileRef != job.AgentProfileRef
                || existing.AgentProfileVersion != job.AgentProfileVersion
                || existing.MaxAttempts != job.MaxAttempts || existing.Deadline != job.Deadline
                || existing.CreatedAt != job.CreatedAt || existing.MultiInstanceExecutionId != job.MultiInstanceExecutionId
                || existing.MultiInstanceIndex != job.MultiInstanceIndex)
                throw new InvalidOperationException("external_task_scheduling_conflict");
            return existing;
        }
        // The owner's concurrency token makes cancellation and competing entry commits conflict.
        owner.Revision++;
        db.ExecutionTokens.Add(wait);
        db.ExternalTaskJobs.Add(job);
        db.HistoryEvents.Add(new HistoryEvent
        {
            Id = Guid.NewGuid(), ProcessInstanceId = job.ProcessInstanceId,
            TenantId = job.TenantId, ElementId = job.ActivityId,
            EventType = "EXTERNAL_TASK_CREATED",
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(job.CreatedAt).UtcDateTime,
            Data = System.Text.Json.JsonSerializer.Serialize(new { jobId = job.Id, job.ActivityExecutionId })
        });
        return job;
    }
}
