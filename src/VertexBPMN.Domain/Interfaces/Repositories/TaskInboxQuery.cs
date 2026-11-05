namespace VertexBPMN.Domain.Interfaces.Repositories;

/// <summary>
/// Bounded query for a user's personal task inbox. The tenant and the requesting
/// identity are supplied by the server (from claims), never chosen by the caller,
/// so a response can never cross a tenant or owner boundary.
/// </summary>
public sealed record TaskInboxQuery(
	string TenantId,
	string UserId,
	IReadOnlyCollection<string> Roles,
	string? Search = null,
	int Page = 1,
	int PageSize = 20)
{
	/// <summary>
	/// Upper bound for a single page. Pagination is fixed server-side to prevent
	/// callers from requesting unbounded result sets.
	/// </summary>
	public const int MaxPageSize = 100;

	public int EffectivePage => Math.Max(1, Page);

	public int EffectivePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}
