using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskHeartbeatResult(
	Guid JobId,
	long LeaseGeneration,
	long LeaseExpiresAt,
	long ServerTime);
