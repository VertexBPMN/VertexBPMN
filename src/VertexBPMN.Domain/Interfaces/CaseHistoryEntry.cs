using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public sealed record CaseHistoryEntry(
	Guid CaseInstanceId,
	IReadOnlyDictionary<string, object?> CaseFile,
	IReadOnlyList<string> CompletedPlanItems,
	DateTime Timestamp);
