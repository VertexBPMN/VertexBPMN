using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskFailCommand(
	Guid LeaseId,
	long LeaseGeneration,
	Guid FailureId,
	string Kind,
	string Code);
