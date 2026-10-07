using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public sealed record ExternalTaskContinuationBatchResult(
	int ContinuationsApplied,
	int DispatchesCompleted,
	int Conflicts);
