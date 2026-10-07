using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskHeartbeatCommand(
	Guid LeaseId,
	long LeaseGeneration,
	int LeaseSeconds = 60);
