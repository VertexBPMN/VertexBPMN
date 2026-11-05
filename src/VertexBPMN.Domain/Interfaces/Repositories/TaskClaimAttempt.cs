namespace VertexBPMN.Domain.Interfaces.Repositories;

/// <summary>
/// A server-side claim attempt. The assignee (<see cref="UserId"/>) is always the
/// authenticated caller derived from claims; there is no caller-chosen assignee.
/// </summary>
public sealed record TaskClaimAttempt(
	Guid TaskId,
	string UserId,
	string TenantId,
	long ExpectedRevision);
