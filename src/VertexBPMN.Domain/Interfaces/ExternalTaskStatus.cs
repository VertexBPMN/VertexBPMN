namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskStatus(
	Guid JobId,
	Guid ActivityExecutionId,
	string Topic,
	string State,
	string ContractVersion,
	string? AgentProfileVersion,
	long Deadline,
	int AttemptsStarted,
	int MaxAttempts,
	long LeaseGeneration,
	long? LeaseExpiresAt,
	string? ApplicationReleaseId = null,
	int FlowRevision = 0);
