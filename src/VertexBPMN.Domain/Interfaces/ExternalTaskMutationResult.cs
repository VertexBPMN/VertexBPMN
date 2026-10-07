using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskMutationResult(
	Guid JobId,
	string State,
	string? ContinuationState,
	long? AvailableAt,
	int AttemptsStarted);
