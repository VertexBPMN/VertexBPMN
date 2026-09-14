using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;

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
                && ((job.State == ExternalTaskState.Ready && job.AvailableAt <= now)
                    || (job.State == ExternalTaskState.Leased && job.LeaseExpiresAt <= now))
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
            throw new ExternalTaskLeaseException("lease_lost");
        }
        await transaction.CommitAsync(cancellationToken);
        return new ExternalTaskHeartbeatResult(jobId, command.LeaseGeneration, expires, now);
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
        var candidateIsReady = candidate.State == ExternalTaskState.Ready;
        var jobUpdated = waitUpdated == 1
            ? await db.ExternalTaskJobs.Where(job => job.Id == candidate.Id && job.Revision == candidate.Revision
                    && ((candidateIsReady && job.State == ExternalTaskState.Ready && job.AvailableAt <= now)
                        || (!candidateIsReady && job.State == ExternalTaskState.Leased && job.LeaseExpiresAt <= now))
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
        if (!candidateIsReady && candidate.LeaseId is { } expiredLeaseId)
        {
            await db.ExternalTaskAttempts.Where(item => item.JobId == candidate.Id
                    && item.LeaseId == expiredLeaseId && item.LeaseGeneration == candidate.LeaseGeneration
                    && item.EndedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.EndedAt, now)
                    .SetProperty(item => item.EndReason, "lease_expired")
                    .SetProperty(item => item.ErrorCode, "lease_expired"), cancellationToken);
        }
        db.ExternalTaskAttempts.Add(new ExternalTaskAttempt
        {
            Id = Guid.NewGuid(), JobId = candidate.Id, AttemptNumber = attempt,
            LeaseId = leaseId, LeaseGeneration = generation, WorkerIssuer = worker.Issuer,
            WorkerSubject = worker.Subject, StartedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
            if (current.Version != job.ContractVersion || current.AgentProfileVersion != job.AgentProfileVersion)
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

    private long Now() => (timeProvider ?? TimeProvider.System).GetUtcNow().ToUnixTimeMilliseconds();

    private sealed class BlockedExternalTaskWorker
    {
        public string Issuer { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
    }
}
