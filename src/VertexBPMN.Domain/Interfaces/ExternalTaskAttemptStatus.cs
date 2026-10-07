using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskAttemptStatus(
	Guid AttemptId,
	int AttemptNumber,
	long LeaseGeneration,
	long StartedAt,
	long? EndedAt,
	string? EndReason,
	string? ErrorCode);
