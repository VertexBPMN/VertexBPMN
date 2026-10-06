using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskCompleteCommand(
	Guid LeaseId,
	long LeaseGeneration,
	Guid CompletionId,
	JsonElement Result);
