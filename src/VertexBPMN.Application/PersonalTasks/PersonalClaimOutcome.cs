namespace VertexBPMN.Application.PersonalTasks;

/// <summary>
/// Outcome of a server-side personal claim, translated for HTTP mapping.
/// </summary>
public enum PersonalClaimOutcome
{
	Claimed,
	NotFound,
	NotCandidate,
	WrongState,
	AlreadyAssigned,
	StaleRevision,
}
