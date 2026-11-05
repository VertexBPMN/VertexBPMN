using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application.PersonalTasks;

/// <summary>
/// Default implementation of <see cref="IPersonalTaskService"/> delegating to the
/// persistence layer for the bounded query and the atomic claim fence.
/// </summary>
public sealed class PersonalTaskService : IPersonalTaskService
{
	private readonly ITaskRepository _repo;

	public PersonalTaskService(ITaskRepository repo) => _repo = repo;

	public ValueTask<PagedTaskInbox> QueryInboxAsync(TaskInboxQuery query, CancellationToken cancellationToken = default)
		=> _repo.QueryInboxAsync(query, cancellationToken);

	public ValueTask<TaskInboxRow?> GetVisibleDetailAsync(
		Guid taskId,
		string tenantId,
		string userId,
		IReadOnlyCollection<string> roles,
		CancellationToken cancellationToken = default)
	{
		return ReadDetailAsync(taskId, tenantId, userId, roles, cancellationToken);
	}

	private async ValueTask<TaskInboxRow?> ReadDetailAsync(
		Guid taskId,
		string tenantId,
		string userId,
		IReadOnlyCollection<string> roles,
		CancellationToken cancellationToken)
	{
		var task = await _repo.GetByIdAsync(taskId, cancellationToken);
		if (task is null
			|| !string.Equals(task.TenantId, tenantId, StringComparison.Ordinal)
			|| !IsVisibleCandidate(task, userId, roles))
		{
			return null;
		}

		return ToRow(task);
	}

	public ValueTask<PersonalClaimResult> ClaimForSelfAsync(
		Guid taskId,
		string tenantId,
		string userId,
		IReadOnlyCollection<string> roles,
		CancellationToken cancellationToken = default)
	{
		return ClaimAsync(taskId, tenantId, userId, roles, cancellationToken);
	}

	private async ValueTask<PersonalClaimResult> ClaimAsync(
		Guid taskId,
		string tenantId,
		string userId,
		IReadOnlyCollection<string> roles,
		CancellationToken cancellationToken)
	{
		var task = await _repo.GetByIdAsync(taskId, cancellationToken);
		if (task is null
			|| !string.Equals(task.TenantId, tenantId, StringComparison.Ordinal))
		{
			return new PersonalClaimResult(PersonalClaimOutcome.NotFound);
		}

		if (!IsVisibleCandidate(task, userId, roles))
		{
			return new PersonalClaimResult(PersonalClaimOutcome.NotCandidate);
		}

		// Fence on the revision the server just observed; the caller never supplies it.
		var outcome = await _repo.TryClaimAsync(
			new TaskClaimAttempt(taskId, userId, tenantId, task.Revision), cancellationToken);

		return new PersonalClaimResult(Map(outcome));
	}

	private static PersonalClaimOutcome Map(TaskClaimOutcome outcome)
		=> outcome switch
		{
			TaskClaimOutcome.Claimed => PersonalClaimOutcome.Claimed,
			TaskClaimOutcome.NotFound => PersonalClaimOutcome.NotFound,
			TaskClaimOutcome.NotCandidate => PersonalClaimOutcome.NotCandidate,
			TaskClaimOutcome.WrongState => PersonalClaimOutcome.WrongState,
			TaskClaimOutcome.AlreadyAssigned => PersonalClaimOutcome.AlreadyAssigned,
			TaskClaimOutcome.StaleRevision => PersonalClaimOutcome.StaleRevision,
			_ => PersonalClaimOutcome.NotFound,
		};

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

	internal static bool IsVisibleCandidate(UserTask task, string userId, IReadOnlyCollection<string> roles)
		=> string.Equals(task.Assignee, userId, StringComparison.Ordinal)
			|| task.CandidateUsers.Contains(userId, StringComparer.Ordinal)
			|| (!string.IsNullOrEmpty(task.CandidateRole) && roles.Contains(task.CandidateRole, StringComparer.Ordinal));
}
