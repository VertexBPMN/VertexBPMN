using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record ClaimedPushOutcome(SourceControlOperationState State, PushReceipt? Receipt,
	SourceControlErrorCode? Error);
