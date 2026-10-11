using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record ClaimedPullRequestOutcome(SourceControlOperationState State, PullRequestReceipt? Receipt,
	SourceControlErrorCode? Error);
