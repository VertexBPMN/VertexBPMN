using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Infrastructure.Operational;

namespace VertexBPMN.Infrastructure.Persistence;

/// <summary>Short, database-only lease transactions. No worker or model I/O occurs here.</summary>
public sealed class ExternalTaskLeaseService(
    BpmnDbContext db,
    IExternalTaskContractResolver contracts,
    TimeProvider? timeProvider = null,
    IConfiguration? configuration = null) : IExternalTaskLeaseService
{
    public async ValueTask<ExternalTaskStatus> GetAsync(
        ExternalTaskWorkerContext worker, Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await FindVisibleBoundJobAsync(worker, jobId, cancellationToken);
        return new ExternalTaskStatus(job.Id, job.ActivityExecutionId, job.Topic, job.State.ToString(),
            job.ContractVersion, job.AgentProfileVersion, job.Deadline, job.AttemptsStarted, job.MaxAttempts,
            job.LeaseGeneration, job.LeaseExpiresAt);
    }

    public async ValueTask<ExternalTaskAttemptPage> GetAttemptsAsync(
        ExternalTaskWorkerContext worker, Guid jobId, string? after, int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100) throw new ExternalTaskLeaseException("invalid_limits");
        _ = await FindVisibleBoundJobAsync(worker, jobId, cancellationToken);
        var cursor = DecodeCursor(after);
        var attempts = await db.ExternalTaskAttempts.AsNoTracking()
            .Where(item => item.JobId == jobId && item.WorkerIssuer == worker.Issuer
                && item.WorkerSubject == worker.Subject
                && (cursor == null || item.AttemptNumber > cursor.Value.Number))
            .OrderBy(item => item.AttemptNumber).ThenBy(item => item.Id)
            .Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = attempts.Count > limit;
        if (hasMore) attempts.RemoveAt(attempts.Count - 1);
        var items = attempts.Select(item => new ExternalTaskAttemptStatus(item.Id, item.AttemptNumber,
            item.LeaseGeneration, item.StartedAt, item.EndedAt, item.EndReason, item.ErrorCode)).ToArray();
        var next = hasMore && attempts.Count > 0 ? EncodeCursor(attempts[^1]) : null;
        return new ExternalTaskAttemptPage(items, next);
    }

    public async ValueTask<IReadOnlyList<ExternalTaskLease>> ClaimAsync(
        ExternalTaskWorkerContext worker,
        ExternalTaskClaimCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateWorker(worker);
        if (command.Topics.Count is < 1 or > 16 || command.MaxTasks is < 1 or > 10
            || command.LeaseSeconds is < 10 or > 120
            || command.Topics.Any(string.IsNullOrWhiteSpace)
            || command.Topics.Distinct(StringComparer.Ordinal).Count() != command.Topics.Count)
            throw new ExternalTaskLeaseException("invalid_limits");
        if (command.Topics.Any(topic => !worker.Topics.Contains(topic)))
            throw new ExternalTaskLeaseException("topic_forbidden");

        var requestedTopics = command.Topics.ToArray();
        var now = Now();
        var candidates = await db.ExternalTaskJobs.AsNoTracking()
            .Where(job => job.TenantId == worker.TenantId
                && requestedTopics.Contains(job.Topic)
                && job.State == ExternalTaskState.Ready && job.AvailableAt <= now
                && job.Deadline > now
                && job.AttemptsStarted < job.MaxAttempts)
            .OrderBy(job => job.AvailableAt).ThenBy(job => job.Id)
            .Take(command.MaxTasks * 4)
            .ToListAsync(cancellationToken);
        var leases = new List<ExternalTaskLease>(command.MaxTasks);
        var remainingResponseBytes = 1024 * 1024 - 64;
        foreach (var candidate in candidates)
        {
            if (leases.Count == command.MaxTasks) break;
            if (!CanUseProfile(worker, candidate.AgentProfileRef)) continue;
            var candidateBytes = System.Text.Encoding.UTF8.GetByteCount(candidate.InputSnapshot)
                + System.Text.Encoding.UTF8.GetByteCount(candidate.SchemaSnapshot) + 2048;
            if (candidateBytes > remainingResponseBytes) continue;
            await EnsurePolicyAsync(candidate, cancellationToken);
            var claimed = await TryClaimAsync(worker, candidate, command.LeaseSeconds, cancellationToken);
            if (claimed is not null)
            {
                leases.Add(claimed);
                remainingResponseBytes -= candidateBytes;
            }
        }
        return leases;
    }

    public async ValueTask<ExternalTaskHeartbeatResult> HeartbeatAsync(
        ExternalTaskWorkerContext worker,
        Guid jobId,
        ExternalTaskHeartbeatCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateWorker(worker);
        if (jobId == Guid.Empty || command.LeaseId == Guid.Empty || command.LeaseGeneration < 1
            || command.LeaseSeconds is < 10 or > 120)
            throw new ExternalTaskLeaseException("invalid_limits");
        var visible = await db.ExternalTaskJobs.AsNoTracking().SingleOrDefaultAsync(job =>
            job.Id == jobId && job.TenantId == worker.TenantId && worker.Topics.Contains(job.Topic), cancellationToken);
        if (visible is null || !CanUseProfile(worker, visible.AgentProfileRef))
            throw new ExternalTaskLeaseException("external_task_not_found");
        if (visible.WorkerIssuer != worker.Issuer || visible.WorkerSubject != worker.Subject)
            throw new ExternalTaskLeaseException("external_task_not_found");
        await EnsurePolicyAsync(visible, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = Now();
        if (now >= visible.Deadline)
            throw new ExternalTaskLeaseException("deadline_exceeded");
        var process = await db.ProcessInstances.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == visible.ProcessInstanceId && item.TenantId == worker.TenantId, cancellationToken);
        var wait = await db.ExecutionTokens.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == visible.WaitTokenId && item.ProcessInstanceId == visible.ProcessInstanceId, cancellationToken);
        if (process?.Status != ProcessInstanceStatus.Running || wait?.State != ExecutionToken.WaitingState)
            throw new ExternalTaskLeaseException("activity_not_waiting");
        var processUpdated = await db.ProcessInstances.Where(item => item.Id == process.Id && item.Revision == process.Revision
                && item.Status == ProcessInstanceStatus.Running)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken);
        var waitUpdated = processUpdated == 1
            ? await db.ExecutionTokens.Where(item => item.Id == wait.Id && item.Revision == wait.Revision
                    && item.State == ExecutionToken.WaitingState)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken)
            : 0;
        var expires = Math.Min(visible.Deadline, checked(now + command.LeaseSeconds * 1000L));
        var jobUpdated = waitUpdated == 1
            ? await db.ExternalTaskJobs.Where(job => job.Id == jobId && job.Revision == visible.Revision
                    && job.State == ExternalTaskState.Leased && job.LeaseId == command.LeaseId
                    && job.LeaseGeneration == command.LeaseGeneration && job.LeaseExpiresAt > now
                    && job.Deadline > now && job.WorkerIssuer == worker.Issuer && job.WorkerSubject == worker.Subject)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.LeaseExpiresAt, expires)
                    .SetProperty(job => job.Revision, job => job.Revision + 1), cancellationToken)
            : 0;
        if (jobUpdated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw LeaseLost();
        }
        await transaction.CommitAsync(cancellationToken);
        return new ExternalTaskHeartbeatResult(jobId, command.LeaseGeneration, expires, now);
    }

    public async ValueTask<ExternalTaskMutationResult> CompleteAsync(
        ExternalTaskWorkerContext worker, Guid jobId, ExternalTaskCompleteCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateWorker(worker);
        if (jobId == Guid.Empty || command.LeaseId == Guid.Empty || command.LeaseGeneration < 1
            || command.CompletionId == Guid.Empty)
            throw new ExternalTaskLeaseException("invalid_limits");
        var visible = await FindVisibleJobAsync(worker, jobId, cancellationToken);
        if (visible.State == ExternalTaskState.Completed)
        {
            if (visible.WorkerIssuer != worker.Issuer || visible.WorkerSubject != worker.Subject)
                throw new ExternalTaskLeaseException("external_task_not_found");
            if (visible.LeaseId != command.LeaseId || visible.LeaseGeneration != command.LeaseGeneration
                || visible.CompletionId != command.CompletionId)
                throw new ExternalTaskLeaseException("completion_conflict");
            await EnsurePolicyAsync(visible, cancellationToken);
            string receiptHash;
            try { (_, receiptHash) = ExternalTaskPayloadPolicy.ValidateResult(command.Result, visible.SchemaSnapshot); }
            catch (ExternalTaskPayloadException exception) { throw new ExternalTaskLeaseException(exception.Code); }
            if (visible.ResultHash != receiptHash) throw new ExternalTaskLeaseException("completion_conflict");
            return await MutationResultAsync(visible, cancellationToken);
        }
        EnsureCurrentLease(visible, worker, command.LeaseId, command.LeaseGeneration);
        await EnsurePolicyAsync(visible, cancellationToken);
        string canonical;
        string resultHash;
        try { (canonical, resultHash) = ExternalTaskPayloadPolicy.ValidateResult(command.Result, visible.SchemaSnapshot); }
        catch (ExternalTaskPayloadException exception)
        {
            RuntimeTelemetry.ExternalTaskSchemaFailures.Add(1);
            throw new ExternalTaskLeaseException(exception.Code);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = Now();
        if (now >= visible.Deadline) throw new ExternalTaskLeaseException("deadline_exceeded");
        var (process, wait) = await LoadActiveOwnersAsync(visible, worker.TenantId, cancellationToken);
        var ownersUpdated = await TouchOwnersAsync(process, wait, cancellationToken);
        var jobUpdated = ownersUpdated
            ? await db.ExternalTaskJobs.Where(job => job.Id == jobId && job.Revision == visible.Revision
                    && job.State == ExternalTaskState.Leased && job.LeaseId == command.LeaseId
                    && job.LeaseGeneration == command.LeaseGeneration && job.LeaseExpiresAt > now
                    && job.Deadline > now && job.WorkerIssuer == worker.Issuer && job.WorkerSubject == worker.Subject)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.State, ExternalTaskState.Completed)
                    .SetProperty(job => job.Result, canonical)
                    .SetProperty(job => job.ResultHash, resultHash)
                    .SetProperty(job => job.CompletionId, command.CompletionId)
                    .SetProperty(job => job.CompletedAt, now)
                    .SetProperty(job => job.ErrorCode, (string?)null)
                    .SetProperty(job => job.Revision, job => job.Revision + 1), cancellationToken)
            : 0;
        if (jobUpdated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw LeaseLost();
        }
        var attemptUpdated = await db.ExternalTaskAttempts.Where(item => item.JobId == jobId
                && item.LeaseId == command.LeaseId && item.LeaseGeneration == command.LeaseGeneration
                && item.EndedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.EndedAt, now)
                .SetProperty(item => item.EndReason, "completed"), cancellationToken);
        if (attemptUpdated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw LeaseLost();
        }
        db.ExternalTaskContinuations.Add(new ExternalTaskContinuation
        {
            Id = Guid.NewGuid(), JobId = jobId, ActivityExecutionId = visible.ActivityExecutionId,
            Outcome = ExternalTaskOutcome.Success, State = ExternalTaskContinuationState.Pending,
            Revision = 1, CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        RuntimeTelemetry.ExternalTaskDuration.Record(Math.Max(0, now - visible.CreatedAt));
        return new ExternalTaskMutationResult(jobId, ExternalTaskState.Completed.ToString(),
            ExternalTaskContinuationState.Pending.ToString(), null, visible.AttemptsStarted);
    }

    public async ValueTask<ExternalTaskMutationResult> FailAsync(
        ExternalTaskWorkerContext worker, Guid jobId, ExternalTaskFailCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateWorker(worker);
        if (jobId == Guid.Empty || command.LeaseId == Guid.Empty || command.LeaseGeneration < 1
            || command.FailureId == Guid.Empty || string.IsNullOrWhiteSpace(command.Kind)
            || string.IsNullOrWhiteSpace(command.Code) || command.Code.Length > 128)
            throw new ExternalTaskLeaseException("invalid_limits");
        var kind = command.Kind.Trim().ToLowerInvariant();
        var code = command.Code.Trim();
        if (kind is not ("technical" or "business") || !ValidErrorCode(code))
            throw new ExternalTaskLeaseException("invalid_request");
        var visible = await FindVisibleJobAsync(worker, jobId, cancellationToken);
        var failureHash = ExternalTaskPayloadPolicy.HashFailure(kind, code);
        var receipt = await db.ExternalTaskAttempts.AsNoTracking().SingleOrDefaultAsync(item =>
            item.JobId == jobId && item.FailureId == command.FailureId, cancellationToken);
        if (receipt is not null)
        {
            if (receipt.WorkerIssuer != worker.Issuer || receipt.WorkerSubject != worker.Subject)
                throw new ExternalTaskLeaseException("external_task_not_found");
            if (receipt.LeaseId != command.LeaseId || receipt.LeaseGeneration != command.LeaseGeneration
                || receipt.FailureHash != failureHash)
                throw new ExternalTaskLeaseException("completion_conflict");
            await EnsurePolicyAsync(visible, cancellationToken);
            return await MutationResultAsync(visible, cancellationToken);
        }
        if (visible.State != ExternalTaskState.Leased)
            throw new ExternalTaskLeaseException("job_terminal");
        EnsureCurrentLease(visible, worker, command.LeaseId, command.LeaseGeneration);
        await EnsurePolicyAsync(visible, cancellationToken);
        if (kind == "business")
        {
            try
            {
                if (!ExternalTaskPayloadPolicy.IsAllowedBusinessError(visible.SchemaSnapshot, code))
                    throw new ExternalTaskLeaseException("business_error_not_allowed");
            }
            catch (ExternalTaskPayloadException exception) { throw new ExternalTaskLeaseException(exception.Code); }
        }
        else if (!RetryableTechnicalCodes.Contains(code) && !TerminalTechnicalCodes.Contains(code))
            throw new ExternalTaskLeaseException("invalid_request");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = Now();
        if (now >= visible.Deadline) throw new ExternalTaskLeaseException("deadline_exceeded");
        var retryAt = checked(now + RetryDelayMilliseconds(visible.Id, visible.AttemptsStarted));
        var retry = kind == "technical" && RetryableTechnicalCodes.Contains(code)
            && visible.AttemptsStarted < visible.MaxAttempts && retryAt < visible.Deadline;
        var target = retry ? ExternalTaskState.RetryScheduled : ExternalTaskState.Failed;
        var (process, wait) = await LoadActiveOwnersAsync(visible, worker.TenantId, cancellationToken);
        var ownersUpdated = await TouchOwnersAsync(process, wait, cancellationToken);
        var targetQuery = db.ExternalTaskJobs.Where(job => job.Id == jobId && job.Revision == visible.Revision
            && job.State == ExternalTaskState.Leased && job.LeaseId == command.LeaseId
            && job.LeaseGeneration == command.LeaseGeneration && job.LeaseExpiresAt > now
            && job.Deadline > now && job.WorkerIssuer == worker.Issuer && job.WorkerSubject == worker.Subject);
        var jobUpdated = !ownersUpdated ? 0 : retry
            ? await targetQuery.ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.State, ExternalTaskState.RetryScheduled)
                .SetProperty(job => job.AvailableAt, retryAt)
                .SetProperty(job => job.ErrorCode, code)
                .SetProperty(job => job.CompletedAt, (long?)null)
                .SetProperty(job => job.Revision, job => job.Revision + 1), cancellationToken)
            : await targetQuery.ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.State, ExternalTaskState.Failed)
                .SetProperty(job => job.ErrorCode, code)
                .SetProperty(job => job.CompletedAt, now)
                .SetProperty(job => job.Revision, job => job.Revision + 1), cancellationToken);
        if (jobUpdated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw LeaseLost();
        }
        var attemptUpdated = await db.ExternalTaskAttempts.Where(item => item.JobId == jobId
                && item.LeaseId == command.LeaseId && item.LeaseGeneration == command.LeaseGeneration
                && item.EndedAt == null && item.FailureId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.EndedAt, now)
                .SetProperty(item => item.EndReason, retry ? "retry_scheduled" : kind)
                .SetProperty(item => item.ErrorCode, code).SetProperty(item => item.FailureId, command.FailureId)
                .SetProperty(item => item.FailureHash, failureHash), cancellationToken);
        if (attemptUpdated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw LeaseLost();
        }
        if (!retry)
            db.ExternalTaskContinuations.Add(new ExternalTaskContinuation
            {
                Id = Guid.NewGuid(), JobId = jobId, ActivityExecutionId = visible.ActivityExecutionId,
                Outcome = kind == "business" ? ExternalTaskOutcome.BusinessError : ExternalTaskOutcome.TechnicalFailure,
                State = ExternalTaskContinuationState.Pending, Revision = 1, CreatedAt = now
            });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (retry) RuntimeTelemetry.ExternalTaskRetries.Add(1);
        else RuntimeTelemetry.ExternalTaskDuration.Record(Math.Max(0, now - visible.CreatedAt));
        return new ExternalTaskMutationResult(jobId, target.ToString(),
            retry ? null : ExternalTaskContinuationState.Pending.ToString(), retry ? retryAt : null,
            visible.AttemptsStarted);
    }

    private static readonly HashSet<string> RetryableTechnicalCodes = new(StringComparer.Ordinal)
        { "provider_unavailable", "provider_rate_limited", "transport_failure", "lease_expired" };
    private static readonly HashSet<string> TerminalTechnicalCodes = new(StringComparer.Ordinal)
        { "invalid_input", "result_validation_exhausted", "budget_exhausted" };

    private async ValueTask<ExternalTaskJob> FindVisibleJobAsync(
        ExternalTaskWorkerContext worker, Guid jobId, CancellationToken cancellationToken)
    {
        var job = await db.ExternalTaskJobs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == jobId && item.TenantId == worker.TenantId && worker.Topics.Contains(item.Topic), cancellationToken);
        if (job is null || !CanUseProfile(worker, job.AgentProfileRef))
            throw new ExternalTaskLeaseException("external_task_not_found");
        return job;
    }

    private static void EnsureCurrentLease(ExternalTaskJob job, ExternalTaskWorkerContext worker,
        Guid leaseId, long leaseGeneration)
    {
        if (job.WorkerIssuer != worker.Issuer || job.WorkerSubject != worker.Subject)
            throw new ExternalTaskLeaseException("external_task_not_found");
        if (job.State != ExternalTaskState.Leased || job.LeaseId != leaseId
            || job.LeaseGeneration != leaseGeneration)
            throw LeaseLost();
    }

    private async ValueTask<(ProcessInstance Process, ExecutionToken Wait)> LoadActiveOwnersAsync(
        ExternalTaskJob job, string tenantId, CancellationToken cancellationToken)
    {
        var process = await db.ProcessInstances.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == job.ProcessInstanceId && item.TenantId == tenantId, cancellationToken);
        var wait = await db.ExecutionTokens.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == job.WaitTokenId && item.ProcessInstanceId == job.ProcessInstanceId
            && item.ActivityExecutionId == job.ActivityExecutionId, cancellationToken);
        if (process?.Status != ProcessInstanceStatus.Running || wait?.State != ExecutionToken.WaitingState)
            throw new ExternalTaskLeaseException("activity_not_waiting");
        return (process, wait);
    }

    private async ValueTask<bool> TouchOwnersAsync(ProcessInstance process, ExecutionToken wait,
        CancellationToken cancellationToken)
    {
        var processUpdated = await db.ProcessInstances.Where(item => item.Id == process.Id
                && item.Revision == process.Revision && item.Status == ProcessInstanceStatus.Running)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken);
        if (processUpdated != 1) return false;
        var waitUpdated = await db.ExecutionTokens.Where(item => item.Id == wait.Id
                && item.Revision == wait.Revision && item.State == ExecutionToken.WaitingState)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken);
        return waitUpdated == 1;
    }

    private async ValueTask<ExternalTaskMutationResult> MutationResultAsync(
        ExternalTaskJob job, CancellationToken cancellationToken)
    {
        var continuation = await db.ExternalTaskContinuations.AsNoTracking()
            .Where(item => item.JobId == job.Id).Select(item => (ExternalTaskContinuationState?)item.State)
            .SingleOrDefaultAsync(cancellationToken);
        return new ExternalTaskMutationResult(job.Id, job.State.ToString(), continuation?.ToString(),
            job.State == ExternalTaskState.RetryScheduled ? job.AvailableAt : null, job.AttemptsStarted);
    }

    private static bool ValidErrorCode(string code) => code.Length is >= 1 and <= 128
        && char.IsAsciiLetter(code[0]) && char.IsLower(code[0])
        && code.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.');

    internal static long RetryDelayMilliseconds(Guid jobId, int attemptNumber)
    {
        var exponent = Math.Min(4, Math.Max(0, attemptNumber - 1));
        var baseDelay = Math.Min(60_000, 5_000L << exponent);
        Span<byte> source = stackalloc byte[20];
        jobId.TryWriteBytes(source);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(source[16..], attemptNumber);
        var hash = System.Security.Cryptography.SHA256.HashData(source);
        return baseDelay + System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(hash) % 1001;
    }

    private async Task<ExternalTaskLease?> TryClaimAsync(ExternalTaskWorkerContext worker, ExternalTaskJob candidate,
        int leaseSeconds, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = Now();
        var process = await db.ProcessInstances.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == candidate.ProcessInstanceId && item.TenantId == worker.TenantId, cancellationToken);
        var wait = await db.ExecutionTokens.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == candidate.WaitTokenId && item.ProcessInstanceId == candidate.ProcessInstanceId, cancellationToken);
        if (process?.Status != ProcessInstanceStatus.Running || wait?.State != ExecutionToken.WaitingState)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        var leaseId = Guid.NewGuid();
        var generation = candidate.LeaseGeneration + 1;
        var attempt = candidate.AttemptsStarted + 1;
        var expires = Math.Min(candidate.Deadline, checked(now + leaseSeconds * 1000L));
        var processUpdated = await db.ProcessInstances.Where(item => item.Id == process.Id && item.Revision == process.Revision
                && item.Status == ProcessInstanceStatus.Running)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken);
        var waitUpdated = processUpdated == 1
            ? await db.ExecutionTokens.Where(item => item.Id == wait.Id && item.Revision == wait.Revision
                    && item.State == ExecutionToken.WaitingState)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Revision, item => item.Revision + 1), cancellationToken)
            : 0;
        var jobUpdated = waitUpdated == 1
            ? await db.ExternalTaskJobs.Where(job => job.Id == candidate.Id && job.Revision == candidate.Revision
                    && job.State == ExternalTaskState.Ready && job.AvailableAt <= now
                    && job.Deadline > now
                    && job.AttemptsStarted < job.MaxAttempts)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(job => job.State, ExternalTaskState.Leased)
                    .SetProperty(job => job.LeaseId, leaseId)
                    .SetProperty(job => job.LeaseGeneration, generation)
                    .SetProperty(job => job.LeaseExpiresAt, expires)
                    .SetProperty(job => job.WorkerIssuer, worker.Issuer)
                    .SetProperty(job => job.WorkerSubject, worker.Subject)
                    .SetProperty(job => job.AttemptsStarted, attempt)
                    .SetProperty(job => job.Revision, job => job.Revision + 1), cancellationToken)
            : 0;
        if (jobUpdated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }
        db.ExternalTaskAttempts.Add(new ExternalTaskAttempt
        {
            Id = Guid.NewGuid(), JobId = candidate.Id, AttemptNumber = attempt,
            LeaseId = leaseId, LeaseGeneration = generation, WorkerIssuer = worker.Issuer,
            WorkerSubject = worker.Subject, StartedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        RuntimeTelemetry.ExternalTaskQueueAge.Record(Math.Max(0, now - candidate.CreatedAt));
        return ToLease(candidate, leaseId, generation, expires, attempt);
    }

    private async ValueTask EnsurePolicyAsync(ExternalTaskJob job, CancellationToken cancellationToken)
    {
        try
        {
            var inputs = JsonSerializer.Deserialize<Dictionary<string, object>>(job.InputSnapshot) ?? [];
            var seconds = checked((int)((job.Deadline - job.CreatedAt) / 1000));
            var current = await contracts.ResolveAsync(job.TenantId,
                new ExternalTaskDefinition(job.Topic, job.AgentProfileRef, job.MaxAttempts - 1, seconds), inputs, cancellationToken);
            if (current.Version != job.ContractVersion || current.AgentProfileVersion != job.AgentProfileVersion
                || current.SchemaSnapshot != job.SchemaSnapshot)
                throw new ExternalTaskLeaseException("policy_revoked");
        }
        catch (ExternalTaskLeaseException) { throw; }
        catch (InvalidOperationException) { throw new ExternalTaskLeaseException("policy_revoked"); }
    }

    private async ValueTask<ExternalTaskJob> FindVisibleBoundJobAsync(
        ExternalTaskWorkerContext worker, Guid jobId, CancellationToken cancellationToken)
    {
        ValidateWorker(worker);
        if (jobId == Guid.Empty) throw new ExternalTaskLeaseException("invalid_limits");
        var job = await db.ExternalTaskJobs.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == jobId && item.TenantId == worker.TenantId && worker.Topics.Contains(item.Topic), cancellationToken);
        if (job is null || !CanUseProfile(worker, job.AgentProfileRef)
            || job.WorkerIssuer != worker.Issuer || job.WorkerSubject != worker.Subject)
            throw new ExternalTaskLeaseException("external_task_not_found");
        await EnsurePolicyAsync(job, cancellationToken);
        return job;
    }

    private static (int Number, Guid Id)? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor)) return null;
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight((padded.Length + 3) / 4 * 4, '=');
            var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded)).Split(':');
            return parts.Length == 2 && int.TryParse(parts[0], out var number) && Guid.TryParseExact(parts[1], "N", out var id)
                ? (number, id) : throw new FormatException();
        }
        catch (FormatException) { throw new ExternalTaskLeaseException("invalid_limits"); }
    }

    private static string EncodeCursor(ExternalTaskAttempt attempt)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{attempt.AttemptNumber}:{attempt.Id:N}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static ExternalTaskLease ToLease(ExternalTaskJob job, Guid leaseId, long generation, long expires, int attempt)
        => new(job.Id, job.ActivityExecutionId, job.Topic, job.ContractVersion, job.AgentProfileVersion,
            JsonDocument.Parse(job.InputSnapshot).RootElement.Clone(), job.SchemaSnapshot,
            leaseId, generation, expires, job.Deadline, attempt, job.MaxAttempts);

    private void ValidateWorker(ExternalTaskWorkerContext worker)
    {
        if (string.IsNullOrWhiteSpace(worker.Issuer) || string.IsNullOrWhiteSpace(worker.Subject)
            || string.IsNullOrWhiteSpace(worker.TenantId) || worker.Topics.Count == 0
            || worker.Topics.Any(string.IsNullOrWhiteSpace) || worker.Profiles.Any(string.IsNullOrWhiteSpace))
            throw new ExternalTaskLeaseException("worker_forbidden");
        var blocked = configuration?.GetSection("ExternalTasks:BlockedWorkers")
            .Get<BlockedExternalTaskWorker[]>() ?? [];
        if (blocked.Any(item => string.Equals(item.Issuer, worker.Issuer, StringComparison.Ordinal)
            && string.Equals(item.Subject, worker.Subject, StringComparison.Ordinal)))
            throw new ExternalTaskLeaseException("worker_forbidden");
    }

    private static bool CanUseProfile(ExternalTaskWorkerContext worker, string? profile)
        => profile is null || worker.Profiles.Contains(profile);

    private static ExternalTaskLeaseException LeaseLost()
    {
        RuntimeTelemetry.ExternalTaskLeaseLosses.Add(1);
        return new ExternalTaskLeaseException("lease_lost");
    }

    private long Now() => (timeProvider ?? TimeProvider.System).GetUtcNow().ToUnixTimeMilliseconds();

    private sealed class BlockedExternalTaskWorker
    {
        public string Issuer { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
    }
}
