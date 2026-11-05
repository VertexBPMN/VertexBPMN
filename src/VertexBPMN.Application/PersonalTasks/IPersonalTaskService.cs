using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Application.PersonalTasks;

/// <summary>
/// Server-side slice for a user's personal tasks: a bounded, assignee/candidate
/// filtered inbox query, object-level read authorization, and server-side claim
/// semantics. The caller never supplies the tenant or the assignee; both are derived
/// from the authenticated identity and enforced on the server.
/// </summary>
public interface IPersonalTaskService
{
	ValueTask<PagedTaskInbox> QueryInboxAsync(TaskInboxQuery query, CancellationToken cancellationToken = default);

	/// <summary>
	/// Reads a single task only when the caller is allowed to see it: the task must
	/// belong to <paramref name="tenantId"/> and the caller must be the assignee or a
	/// matching candidate. Returns <c>null</c> (never throws, never leaks existence)
	/// when not authorized.
	/// </summary>
	ValueTask<TaskInboxRow?> GetVisibleDetailAsync(
		Guid taskId,
		string tenantId,
		string userId,
		IReadOnlyCollection<string> roles,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Claims a task for the authenticated caller. <paramref name="userId"/> is derived
	/// from claims by the caller of this method (the controller), never supplied by the
	/// client. The caller also never supplies the tenant or a revision: both are resolved
	/// server-side (tenant from claims, revision from the task the server just read) so
	/// the assignee choice and the concurrency fence are always owned by the server.
	/// Enforces candidate rights, open task state, current assignment and a revision-fenced
	/// atomic update.
	/// </summary>
	ValueTask<PersonalClaimResult> ClaimForSelfAsync(
		Guid taskId,
		string tenantId,
		string userId,
		IReadOnlyCollection<string> roles,
		CancellationToken cancellationToken = default);
}
