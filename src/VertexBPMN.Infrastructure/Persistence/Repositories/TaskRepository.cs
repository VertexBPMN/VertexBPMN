using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of ITaskRepository.
/// </summary>
public class TaskRepository : ITaskRepository
{
    private readonly BpmnDbContext _db;
    public TaskRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(UserTask userTask, CancellationToken cancellationToken = default)
    {
        if (_db.Entry(userTask).State == EntityState.Detached)
		{
			_db.Tasks.Update(userTask);
		}

		await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask<UserTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Tasks.FindAsync(new object[] { id }, cancellationToken);
    }

    public async IAsyncEnumerable<UserTask> ListAsync(Guid? processInstanceId = null, string? assignee = null, string? tenantId = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.Tasks.AsQueryable();
        query = query.Where(t => t.Status == UserTaskStatus.Pending || t.Status == UserTaskStatus.Delegated);
        if (processInstanceId != null)
		{
			query = query.Where(t => t.ProcessInstanceId == processInstanceId);
		}

		if (assignee != null)
		{
			query = query.Where(t => t.Assignee == assignee);
		}

		if (tenantId != null)
		{
			query = query.Where(t => t.TenantId == tenantId);
		}

		await foreach (var task in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
		{
			yield return task;
		}
	}

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Tasks.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null)
		{
			_db.Tasks.Remove(entity);
		}
    }

    public async ValueTask<PagedTaskInbox> QueryInboxAsync(TaskInboxQuery query, CancellationToken cancellationToken = default)
    {
        // Tenant and actionable state are filtered in SQL; candidate/assignee/role
        // visibility and free-text search are applied in memory because the relevant
        // collections are stored as JSON columns and cannot be translated to SQL.
        var tenantTasks = await _db.Tasks.AsNoTracking()
            .Where(t => t.TenantId == query.TenantId
                        && (t.Status == UserTaskStatus.Pending || t.Status == UserTaskStatus.Delegated))
            .ToListAsync(cancellationToken);

        var visible = tenantTasks
            .Where(t => IsVisibleTo(t, query.UserId, query.Roles)
                        && (string.IsNullOrEmpty(query.Search)
                            || t.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .ToList();

        var total = visible.Count;
        var page = query.EffectivePage;
        var pageSize = query.EffectivePageSize;

        var items = visible
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToRow)
            .ToList();

        return new PagedTaskInbox(items, total, page, pageSize);
    }

    public async ValueTask<TaskClaimOutcome> TryClaimAsync(TaskClaimAttempt attempt, CancellationToken cancellationToken = default)
    {
        var conflict = await PreCheckAsync(attempt, cancellationToken);
        if (conflict is not null)
        {
            return conflict.Value;
        }

        // Atomic Compare-And-Swap. A single conditional UPDATE can only affect the row
        // when it still has the caller-derived expected revision, is still claimable
        // (non-terminal state), still belongs to the caller's tenant, and is either
        // unassigned or already held by this caller. The statement itself is atomic;
        // it runs inside an explicit transaction so the fence and the write commit
        // together. affected-rows == 1 means we won; 0 means a concurrent claim (or
        // other change) advanced the revision in between, so we lost the race.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var lastModified = DateTime.UtcNow;
        var affected = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE "Tasks"
             SET "Assignee" = {attempt.UserId},
                 "Revision" = "Revision" + 1,
                 "LastModified" = {lastModified},
                 "ModifiedBy" = {attempt.UserId}
             WHERE "Id" = {attempt.TaskId}
               AND "TenantId" = {attempt.TenantId}
               AND "Revision" = {attempt.ExpectedRevision}
               AND "Status" IN ({(int)UserTaskStatus.Pending}, {(int)UserTaskStatus.Delegated})
               AND ("Assignee" IS NULL OR "Assignee" = {attempt.UserId})
             """,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (affected != 1)
        {
            return TaskClaimOutcome.StaleRevision;
        }

        SyncTrackedSnapshot(attempt.TaskId, attempt.UserId, attempt.ExpectedRevision + 1, lastModified);
        return TaskClaimOutcome.Claimed;
    }

    /// <summary>
    /// Read-only pre-check producing deterministic, distinct outcomes for the
    /// non-race cases. This deliberately applies the same revision tie used by
    /// synchronous reads; it performs no write (null means "proceed to the CAS").
    /// </summary>
    private async ValueTask<TaskClaimOutcome?> PreCheckAsync(TaskClaimAttempt attempt, CancellationToken cancellationToken)
    {
        var task = await _db.Tasks.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == attempt.TaskId, cancellationToken);
        if (task is null || !string.Equals(task.TenantId, attempt.TenantId, StringComparison.Ordinal))
        {
            return TaskClaimOutcome.NotFound;
        }

        if (task.Status != UserTaskStatus.Pending && task.Status != UserTaskStatus.Delegated)
        {
            return TaskClaimOutcome.WrongState;
        }

        if (task.Revision != attempt.ExpectedRevision)
        {
            return TaskClaimOutcome.StaleRevision;
        }

        if (task.Assignee != null && !string.Equals(task.Assignee, attempt.UserId, StringComparison.Ordinal))
        {
            return TaskClaimOutcome.AlreadyAssigned;
        }

        return null;
    }

    /// <summary>
    /// The raw CAS bypasses the change tracker, so keep any already-tracked snapshot
    /// consistent with the committed write (e.g. pending inbox rows or the service's
    /// earlier GetByIdAsync read) instead of leaving it stale.
    /// </summary>
    private void SyncTrackedSnapshot(Guid taskId, string userId, long newRevision, DateTime lastModified)
    {
        var tracked = _db.ChangeTracker.Entries<UserTask>().FirstOrDefault(e => e.Entity.Id == taskId);
        if (tracked is null)
        {
            return;
        }

        tracked.Entity.Assignee = userId;
        tracked.Entity.Revision = newRevision;
        tracked.Entity.LastModified = lastModified;
        tracked.Entity.ModifiedBy = userId;
        tracked.State = EntityState.Unchanged;
    }

    private static bool IsVisibleTo(UserTask task, string userId, IReadOnlyCollection<string> roles)
        => string.Equals(task.Assignee, userId, StringComparison.Ordinal)
            || task.CandidateUsers.Contains(userId, StringComparer.Ordinal)
            || (!string.IsNullOrEmpty(task.CandidateRole) && roles.Contains(task.CandidateRole, StringComparer.Ordinal));

    private static TaskInboxRow ToRow(UserTask task)
        => new(
            task.Id,
            task.Name,
            task.Status,
            task.ProcessInstanceId,
            task.TenantId,
            task.CreatedAt,
            task.DueDate,
            task.CompletedAt,
            task.FormKey);
}
