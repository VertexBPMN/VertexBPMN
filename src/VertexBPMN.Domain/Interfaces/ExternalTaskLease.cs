using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskLease(
	Guid JobId,
	Guid ActivityExecutionId,
	string Topic,
	string ContractVersion,
	string? AgentProfileVersion,
	JsonElement Input,
	string SchemaSnapshot,
	Guid LeaseId,
	long LeaseGeneration,
	long LeaseExpiresAt,
	long Deadline,
	int AttemptNumber,
	int MaxAttempts);
