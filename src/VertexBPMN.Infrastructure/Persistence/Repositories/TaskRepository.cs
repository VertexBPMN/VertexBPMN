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
        var task = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == attempt.TaskId, cancellationToken);
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

        // The caller is unassigned or re-claiming their own task: claim it and advance
        // the revision fence so a concurrent claim/snapshot cannot silently win.
        task.Assignee = attempt.UserId;
        task.Revision++;
        task.LastModified = DateTime.UtcNow;
        task.ModifiedBy = attempt.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return TaskClaimOutcome.Claimed;
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
