using System.Text.Json;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskAttemptPage(
	IReadOnlyList<ExternalTaskAttemptStatus> Items,
	string? NextCursor);
