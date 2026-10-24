namespace VertexBPMN.Domain.Interfaces.Repositories;

/// <summary>
/// A single page of the personal inbox with a stable sort (created ascending with
/// the task id as tiebreaker) and the total matching count.
/// </summary>
public sealed record PagedTaskInbox(
	IReadOnlyList<TaskInboxRow> Items,
	int Total,
	int Page,
	int PageSize);
