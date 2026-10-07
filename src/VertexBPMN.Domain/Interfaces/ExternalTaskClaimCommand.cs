using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskClaimCommand(
	IReadOnlyCollection<string> Topics,
	int MaxTasks = 1,
	int LeaseSeconds = 60);
