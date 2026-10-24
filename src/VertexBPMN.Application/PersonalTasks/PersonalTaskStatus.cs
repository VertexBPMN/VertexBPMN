using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Application.PersonalTasks;

/// <summary>
/// Whitelist projection of task state for the personal inbox. Only a controlled set
/// of status strings is ever exposed; raw enum values and, crucially, process
/// variables are never surfaced.
/// </summary>
public static class PersonalTaskStatus
{
	private static readonly HashSet<string> Controlled = new(StringComparer.Ordinal)
	{
		"pending",
		"delegated",
		"completed",
		"rejected",
		"cancelled",
	};

	/// <summary>
	/// The controlled status surface. Intentionally small and string-only; it never
	/// carries process variables or the internal enum values.
	/// </summary>
	public static IReadOnlySet<string> Statuses => Controlled;

	public static string Of(UserTaskStatus status)
		=> status switch
		{
			UserTaskStatus.Pending => "pending",
			UserTaskStatus.Delegated => "delegated",
			UserTaskStatus.Completed => "completed",
			UserTaskStatus.Rejected => "rejected",
			UserTaskStatus.Cancelled => "cancelled",
			_ => "unknown",
		};

	public static bool IsControlled(string status) => Controlled.Contains(status);
}
