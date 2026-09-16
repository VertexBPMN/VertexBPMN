using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Infrastructure.Persistence;

/// <summary>Bounded database-only recovery. Each candidate is linearized independently.</summary>
public sealed class ExternalTaskRecoveryService(BpmnDbContext db, TimeProvider? timeProvider = null)
    : IExternalTaskRecoveryService
{
    public async ValueTask<ExternalTaskRecoveryResult> RecoverAsync(
        int maximumJobs = 100, CancellationToken cancellationToken = default)
    {
        if (maximumJobs is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maximumJobs));
        var now = Now();
        var ids = await db.ExternalTaskJobs.AsNoTracking()
            .Where(job => (job.State == ExternalTaskState.Ready || job.State == ExternalTaskState.Leased
                    || job.State == ExternalTaskState.RetryScheduled)
                && (job.Deadline <= now
                    || (job.State == ExternalTaskState.Leased && job.LeaseExpiresAt <= now)
                    || (job.State == ExternalTaskState.RetryScheduled && job.AvailableAt <= now)))
            .OrderBy(job => job.Deadline).ThenBy(job => job.Id)
            .Select(job => job.Id).Take(maximumJobs).ToListAsync(cancellationToken);
        var transitioned = 0;
        var conflicts = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await RecoverOneAsync(id, cancellationToken)) transitioned++; else conflicts++;
            db.ChangeTracker.Clear();
        }
        return new ExternalTaskRecoveryResult(ids.Count, transitioned, conflicts);
    }

    private async ValueTask<bool> RecoverOneAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = Now();
        var job = await db.ExternalTaskJobs.AsNoTracking().SingleOrDefaultAsync(item => item.Id == jobId, cancellationToken);
        if (job is null || job.State is not (ExternalTaskState.Ready or ExternalTaskState.Leased or ExternalTaskState.RetryScheduled))
            return false;
        var process = await db.ProcessInstances.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == job.ProcessInstanceId && item.TenantId == job.TenantId, cancellationToken);
        var wait = await db.ExecutionTokens.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == job.WaitTokenId && item.ProcessInstanceId == job.ProcessInstanceId
            && item.ActivityExecutionId == job.ActivityExecutionId, cancellationToken);
        if (process?.Status != ProcessInstanceStatus.Running || wait?.State != ExecutionToken.WaitingState)
            return false;

        var processUpdated = await db.ProcessInstances.Where(item => item.Id == process.Id
                && item.Revision == process.Revision && item.Status == ProcessInstanceStatus.Running)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken);
        var waitUpdated = processUpdated == 1
            ? await db.ExecutionTokens.Where(item => item.Id == wait.Id && item.Revision == wait.Revision
                    && item.State == ExecutionToken.WaitingState)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken)
            : 0;
        if (waitUpdated != 1) { await transaction.RollbackAsync(cancellationToken); return false; }

        if (now >= job.Deadline)
            return await MakeTerminalAsync(job, ExternalTaskState.TimedOut, ExternalTaskOutcome.Timeout,
                "external_task_timeout", now, transaction, cancellationToken);
        if (job.State == ExternalTaskState.RetryScheduled)
        {
            if (job.AvailableAt > now) { await transaction.RollbackAsync(cancellationToken); return false; }
            var changed = await db.ExternalTaskJobs.Where(item => item.Id == job.Id && item.Revision == job.Revision
                    && item.State == ExternalTaskState.RetryScheduled && item.AvailableAt <= now && item.Deadline > now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.State, ExternalTaskState.Ready)
                    .SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken);
            if (changed != 1) { await transaction.RollbackAsync(cancellationToken); return false; }
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        if (job.State != ExternalTaskState.Leased || job.LeaseExpiresAt > now)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var attemptUpdated = await db.ExternalTaskAttempts.Where(item => item.JobId == job.Id
                && item.LeaseId == job.LeaseId && item.LeaseGeneration == job.LeaseGeneration && item.EndedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.EndedAt, now)
                .SetProperty(item => item.EndReason, "lease_expired")
                .SetProperty(item => item.ErrorCode, "lease_expired"), cancellationToken);
        if (attemptUpdated != 1) { await transaction.RollbackAsync(cancellationToken); return false; }
        var retryAt = checked(now + ExternalTaskLeaseService.RetryDelayMilliseconds(job.Id, job.AttemptsStarted));
        if (job.AttemptsStarted >= job.MaxAttempts || retryAt >= job.Deadline)
            return await MakeTerminalAsync(job, ExternalTaskState.Failed, ExternalTaskOutcome.TechnicalFailure,
                "external_task_failed", now, transaction, cancellationToken);
        var scheduled = await db.ExternalTaskJobs.Where(item => item.Id == job.Id && item.Revision == job.Revision
                && item.State == ExternalTaskState.Leased && item.LeaseExpiresAt <= now && item.Deadline > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.State, ExternalTaskState.RetryScheduled)
                .SetProperty(item => item.AvailableAt, retryAt).SetProperty(item => item.ErrorCode, "lease_expired")
                .SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken);
        if (scheduled != 1) { await transaction.RollbackAsync(cancellationToken); return false; }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async ValueTask<bool> MakeTerminalAsync(ExternalTaskJob job, ExternalTaskState state,
        ExternalTaskOutcome outcome, string errorCode, long now, IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        var changed = await db.ExternalTaskJobs.Where(item => item.Id == job.Id && item.Revision == job.Revision
                && (item.State == ExternalTaskState.Ready || item.State == ExternalTaskState.Leased
                    || item.State == ExternalTaskState.RetryScheduled))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.State, state)
                .SetProperty(item => item.ErrorCode, errorCode).SetProperty(item => item.CompletedAt, now)
                .SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken);
        if (changed != 1) { await transaction.RollbackAsync(cancellationToken); return false; }
        if (job.State == ExternalTaskState.Leased)
            await db.ExternalTaskAttempts.Where(item => item.JobId == job.Id && item.EndedAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.EndedAt, now)
                    .SetProperty(item => item.EndReason, state == ExternalTaskState.TimedOut ? "deadline" : "lease_expired")
                    .SetProperty(item => item.ErrorCode, errorCode), cancellationToken);
        db.ExternalTaskContinuations.Add(new ExternalTaskContinuation
        {
            Id = Guid.NewGuid(), JobId = job.Id, ActivityExecutionId = job.ActivityExecutionId,
            Outcome = outcome, State = ExternalTaskContinuationState.Pending, Revision = 1, CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private long Now() => (timeProvider ?? TimeProvider.System).GetUtcNow().ToUnixTimeMilliseconds();
}
