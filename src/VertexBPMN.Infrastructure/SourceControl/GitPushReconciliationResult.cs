namespace VertexBPMN.Infrastructure.SourceControl;

internal enum GitPushReconciliationResult
{
	ReadyToPush,
	Confirmed,
	Conflict,
	ResultUnknown
}
