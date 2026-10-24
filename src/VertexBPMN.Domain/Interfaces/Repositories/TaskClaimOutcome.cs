namespace VertexBPMN.Domain.Interfaces.Repositories;

/// <summary>
/// Outcome of a server-side claim attempt. The repository enforces the state,
/// current-assignment and revision fencing rules atomically.
/// </summary>
public enum TaskClaimOutcome
{
	Claimed,
	NotFound,
	NotCandidate,
	WrongState,
	AlreadyAssigned,
	StaleRevision,
}
